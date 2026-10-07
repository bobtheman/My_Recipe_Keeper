using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Free, key-less recipe import. In order of preference:
    ///   1. schema.org Recipe JSON-LD — nearly every recipe site/blog publishes this for Google rich results.
    ///   2. TikTok's public oEmbed endpoint — returns the video caption without scraping.
    ///   3. og:description / meta description run through RecipeTextParser — covers Instagram/Facebook
    ///      captions (fetched with a link-preview UA) and sites with no structured data.
    /// Whatever can't be parsed still comes back with title/image/link so the user can fill in the rest.
    /// Registered as a typed HttpClient, same as Pok_E_List's lookup service.
    /// </summary>
    public class RecipeScraperService : IRecipeScraperService
    {
        private static readonly string[] SocialHosts = { "instagram.com", "facebook.com", "fb.watch", "threads.net", "threads.com" };
        private static readonly string[] TikTokHosts = { "tiktok.com" };

        private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new(@"[ \t ]+", RegexOptions.Compiled);

        private static readonly JsonDocumentOptions JsonOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private readonly HttpClient _httpClient;
        private readonly ScraperOptions _options;
        private readonly ITranslationService? _translationService;

        // Single public constructor on purpose: typed-HttpClient activation throws when two constructors both accept HttpClient.
        public RecipeScraperService(HttpClient httpClient, ScraperOptions options, ITranslationService? translationService = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _translationService = translationService;
        }

        public async Task<ScrapeResult> ScrapeAsync(string urlOrSharedText, string? translateTo = null, CancellationToken ct = default)
        {
            var result = await ScrapeUntranslatedAsync(urlOrSharedText, ct);
            if (!result.Success || translateTo is null || _translationService is null)
            {
                return result;
            }

            try
            {
                return await TranslateAsync(result, translateTo, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return result; // translation is a nice-to-have; keep the original-language recipe
            }
        }

        /// <summary>
        /// Captions are translated *before* parsing — the parser recognises English headings ("For the
        /// dough:", "Method:"), so a Turkish "Hamuru için:" only becomes structure after translation.
        /// Structured-data recipes are already split into fields, so those are translated field by field.
        /// </summary>
        private async Task<ScrapeResult> TranslateAsync(ScrapeResult result, string translateTo, CancellationToken ct)
        {
            if (result.Caption is { } input)
            {
                var caption = input.Caption is null ? null : RecipeTextParser.UnwrapSocialCaption(input.Caption);
                if (string.IsNullOrWhiteSpace(caption))
                {
                    return result;
                }

                var translatedCaption = await _translationService!.TranslateAsync(caption, translateTo, ct);
                if (translatedCaption is null || string.Equals(translatedCaption.SourceLanguage, translateTo, StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }

                // Social og:title just repeats the caption; a real page title needs its own translation.
                var pageTitle = input.PageTitle;
                if (pageTitle is not null && !IsSocialTitle(pageTitle) && !input.PreferCaptionTitle)
                {
                    pageTitle = (await _translationService.TranslateAsync(pageTitle, translateTo, ct))?.Text ?? pageTitle;
                }

                var rebuilt = FromCaption(translatedCaption.Text, pageTitle, input.Image, input.SourceUrl, input.PreferCaptionTitle);
                rebuilt.TranslatedFrom = translatedCaption.SourceLanguage;
                return rebuilt;
            }

            var source = await new RecipeTranslator(_translationService!).TranslateAsync(result.Recipe!, translateTo, ct);
            result.TranslatedFrom = source;
            return result;
        }

        private async Task<ScrapeResult> ScrapeUntranslatedAsync(string urlOrSharedText, CancellationToken ct)
        {
            var url = SharedTextUrlExtractor.Extract(urlOrSharedText);
            if (url is null)
            {
                return ScrapeResult.Fail("Couldn't find a link in that text.");
            }

            var uri = new Uri(url);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                if (IsHost(uri, TikTokHosts))
                {
                    var tikTok = await TryTikTokAsync(url, timeout.Token);
                    if (tikTok is not null)
                    {
                        return tikTok;
                    }
                }

                var userAgent = IsHost(uri, SocialHosts) ? _options.SocialUserAgent : _options.UserAgent;
                var html = await FetchHtmlAsync(uri, userAgent, timeout.Token);
                if (html is null)
                {
                    return ScrapeResult.Fail("That page couldn't be loaded — check the link, or the site may block apps.");
                }

                return ParseHtml(html, url);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ScrapeResult.Fail("The site took too long to respond.");
            }
            catch (HttpRequestException ex)
            {
                return ScrapeResult.Fail($"Couldn't reach the site ({ex.Message}).");
            }
        }

        /// <summary>Public for unit tests: HTML in, recipe out, no network.</summary>
        public static ScrapeResult ParseHtml(string html, string sourceUrl)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var structured = TryJsonLd(document, sourceUrl);
            if (structured is not null)
            {
                return new ScrapeResult { Recipe = structured, Source = ScrapeSource.StructuredData };
            }

            var title = Meta(document, "og:title") ?? Meta(document, "twitter:title")
                ?? CleanText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
            var description = Meta(document, "og:description") ?? Meta(document, "description") ?? Meta(document, "twitter:description");
            var image = Meta(document, "og:image") ?? Meta(document, "twitter:image");

            if (title is null && description is null)
            {
                return ScrapeResult.Fail("No recipe details found on that page.");
            }

            return FromCaption(description, title, image, sourceUrl);
        }

        private async Task<ScrapeResult?> TryTikTokAsync(string url, CancellationToken ct)
        {
            var endpoint = $"{_options.TikTokOEmbedUrl}?url={Uri.EscapeDataString(url)}";
            using var response = await _httpClient.GetAsync(endpoint, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            TikTokOEmbed? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<TikTokOEmbed>(cancellationToken: ct);
            }
            catch (JsonException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(body?.Title))
            {
                return null;
            }

            var fallbackTitle = string.IsNullOrWhiteSpace(body.AuthorName) ? "TikTok recipe" : $"Recipe from {body.AuthorName}";
            return FromCaption(body.Title, fallbackTitle, body.ThumbnailUrl, url, preferCaptionTitle: true);
        }

        private async Task<string?> FetchHtmlAsync(Uri uri, string userAgent, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-GB,en;q=0.9");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }

        private static ScrapeResult FromCaption(string? caption, string? pageTitle, string? image, string sourceUrl, bool preferCaptionTitle = false)
        {
            var parsed = RecipeTextParser.Parse(caption);

            // Social og:title is "Chef on Instagram: <caption>" — the parsed caption's first line is a
            // far better title. Regular sites have a real og:title, so it wins there.
            var socialTitle = pageTitle is not null && IsSocialTitle(pageTitle);
            var titleFromCaption = (preferCaptionTitle || socialTitle || pageTitle is null) && parsed.Title is not null;
            var title = titleFromCaption
                ? parsed.Title!
                : CleanText(socialTitle ? RecipeTextParser.StripSocialPostTitle(pageTitle!) : pageTitle) ?? "Untitled recipe";

            // Parser description skips the caption line it used as the title; if the title came from the
            // page instead (normal websites), keep the whole cleaned caption as the description.
            var description = titleFromCaption || parsed.HasRecipe
                ? parsed.Description
                : CaptionCleaner.CleanBlock(RecipeTextParser.UnwrapSocialCaption(caption ?? string.Empty));

            var recipe = new RecipeEntity
            {
                Title = Truncate(title, 120),
                Description = CleanText(description),
                IngredientsText = RecipeEntity.JoinLines(parsed.Ingredients),
                InstructionsText = RecipeEntity.JoinLines(parsed.Instructions),
                Notes = parsed.Notes.Count > 0 ? string.Join("\n", parsed.Notes) : null,
                ImageUrl = image,
                SourceUrl = sourceUrl
            };

            return new ScrapeResult
            {
                Recipe = recipe,
                Source = parsed.HasRecipe ? ScrapeSource.SocialCaption : ScrapeSource.PageMetadata,
                Caption = new CaptionInput(caption, pageTitle, image, sourceUrl, preferCaptionTitle)
            };
        }

        private static bool IsSocialTitle(string pageTitle) => RecipeTextParser.IsSocialPostTitle(pageTitle);

        private static RecipeEntity? TryJsonLd(HtmlDocument document, string sourceUrl)
        {
            var scripts = document.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (scripts is null)
            {
                return null;
            }

            foreach (var script in scripts)
            {
                // InnerHtml, not InnerText: HtmlAgilityPack's InnerText entity-decodes and would
                // turn &quot; inside JSON strings into bare quotes, breaking the JSON.
                var json = script.InnerHtml.Trim();
                if (json.Length == 0)
                {
                    continue;
                }

                try
                {
                    using var parsed = JsonDocument.Parse(json, JsonOptions);
                    var recipeElement = FindRecipe(parsed.RootElement, depth: 0);
                    if (recipeElement is { } element)
                    {
                        return MapRecipe(element, sourceUrl);
                    }
                }
                catch (JsonException)
                {
                    // Malformed block (surprisingly common) — try the next one.
                }
            }

            return null;
        }

        private static JsonElement? FindRecipe(JsonElement element, int depth)
        {
            if (depth > 5)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindRecipe(item, depth + 1);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (IsRecipeType(element))
            {
                return element;
            }

            foreach (var container in new[] { "@graph", "mainEntity", "itemListElement", "item" })
            {
                if (element.TryGetProperty(container, out var child))
                {
                    var found = FindRecipe(child, depth + 1);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static bool IsRecipeType(JsonElement element)
        {
            if (!element.TryGetProperty("@type", out var type))
            {
                return false;
            }

            static bool Matches(string? value) =>
                value is not null && (value.Equals("Recipe", StringComparison.OrdinalIgnoreCase) || value.EndsWith("/Recipe", StringComparison.OrdinalIgnoreCase) || value.EndsWith(":Recipe", StringComparison.OrdinalIgnoreCase));

            return type.ValueKind switch
            {
                JsonValueKind.String => Matches(type.GetString()),
                JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && Matches(t.GetString())),
                _ => false
            };
        }

        private static RecipeEntity MapRecipe(JsonElement recipe, string sourceUrl)
        {
            var ingredients = StringList(recipe, "recipeIngredient");
            if (ingredients.Count == 0)
            {
                ingredients = StringList(recipe, "ingredients");
            }

            var instructions = new List<string>();
            if (recipe.TryGetProperty("recipeInstructions", out var instructionsElement))
            {
                CollectInstructions(instructionsElement, instructions, depth: 0);
            }

            return new RecipeEntity
            {
                Title = Truncate(CleanText(StringValue(recipe, "name")) ?? "Untitled recipe", 120),
                Description = CleanText(StringValue(recipe, "description")),
                IngredientsText = RecipeEntity.JoinLines(ingredients),
                InstructionsText = RecipeEntity.JoinLines(instructions),
                Servings = Yield(recipe),
                PrepTime = FormatDuration(StringValue(recipe, "prepTime")),
                CookTime = FormatDuration(StringValue(recipe, "cookTime")),
                TotalTime = FormatDuration(StringValue(recipe, "totalTime")),
                ImageUrl = Image(recipe),
                Category = JoinedValue(recipe, "recipeCategory") ?? JoinedValue(recipe, "recipeCuisine"),
                SourceUrl = sourceUrl
            };
        }

        private static void CollectInstructions(JsonElement element, List<string> output, int depth)
        {
            if (depth > 4)
            {
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    // Some sites put the whole method in one HTML/newline-separated string.
                    var text = element.GetString() ?? string.Empty;
                    text = Regex.Replace(text, @"<\s*(br|/p|/li)\s*/?>", "\n", RegexOptions.IgnoreCase);
                    foreach (var line in text.Split('\n'))
                    {
                        var cleaned = CleanText(line);
                        if (cleaned is not null)
                        {
                            output.Add(cleaned);
                        }
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectInstructions(item, output, depth + 1);
                    }

                    break;

                case JsonValueKind.Object:
                    if (element.TryGetProperty("itemListElement", out var steps))
                    {
                        // HowToSection: its name becomes a group label ("For the sauce:").
                        var sectionName = CleanText(StringValue(element, "name"));
                        if (sectionName is not null)
                        {
                            output.Add(sectionName.TrimEnd(':') + ":");
                        }

                        CollectInstructions(steps, output, depth + 1);
                    }
                    else
                    {
                        var stepText = CleanText(StringValue(element, "text")) ?? CleanText(StringValue(element, "name"));
                        if (stepText is not null)
                        {
                            output.Add(stepText);
                        }
                    }

                    break;
            }
        }

        private static string? Yield(JsonElement recipe)
        {
            if (!recipe.TryGetProperty("recipeYield", out var value))
            {
                return null;
            }

            string? raw = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                // ["4", "4 servings"] is common — the most descriptive entry is the longest one.
                JsonValueKind.Array => value.EnumerateArray()
                    .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .OrderByDescending(v => v!.Length)
                    .FirstOrDefault(),
                _ => null
            };

            raw = CleanText(raw);
            if (raw is null)
            {
                return null;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? $"{count} servings"
                : raw;
        }

        private static string? Image(JsonElement recipe)
        {
            if (!recipe.TryGetProperty("image", out var image))
            {
                return null;
            }

            return ImageUrl(image);

            static string? ImageUrl(JsonElement element) => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Array => element.EnumerateArray().Select(ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                JsonValueKind.Object => element.TryGetProperty("url", out var url) ? ImageUrl(url) : null,
                _ => null
            };
        }

        /// <summary>ISO 8601 duration ("PT1H30M") → "1 hr 30 mins". Unparseable values are returned as-is.</summary>
        public static string? FormatDuration(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
            {
                return null;
            }

            TimeSpan span;
            try
            {
                span = XmlConvert.ToTimeSpan(iso.Trim());
            }
            catch (FormatException)
            {
                return iso.Trim();
            }

            if (span <= TimeSpan.Zero)
            {
                return null;
            }

            var hours = (int)span.TotalHours;
            var minutes = span.Minutes;
            var parts = new List<string>();
            if (hours > 0)
            {
                parts.Add(hours == 1 ? "1 hr" : $"{hours} hrs");
            }

            if (minutes > 0)
            {
                parts.Add(minutes == 1 ? "1 min" : $"{minutes} mins");
            }

            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static List<string> StringList(JsonElement parent, string property)
        {
            var list = new List<string>();
            if (!parent.TryGetProperty(property, out var value))
            {
                return list;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    var text = CleanText(item.ValueKind == JsonValueKind.String ? item.GetString() : StringValue(item, "name"));
                    if (text is not null)
                    {
                        list.Add(text);
                    }
                }
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                var text = CleanText(value.GetString());
                if (text is not null)
                {
                    list.Add(text);
                }
            }

            return list;
        }

        private static string? StringValue(JsonElement parent, string property)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(property, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Array => value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).FirstOrDefault(),
                _ => null
            };
        }

        private static string? JoinedValue(JsonElement parent, string property)
        {
            if (!parent.TryGetProperty(property, out var value))
            {
                return null;
            }

            var joined = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).Distinct()),
                _ => null
            };

            return CleanText(joined);
        }

        private static string? Meta(HtmlDocument document, string key)
        {
            var node = document.DocumentNode.SelectSingleNode($"//meta[@property='{key}']")
                ?? document.DocumentNode.SelectSingleNode($"//meta[@name='{key}']");
            var content = node?.GetAttributeValue("content", string.Empty);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            // Keep newlines: captions rely on them for list structure. HtmlDecode handles &#10; etc.
            return WebUtility.HtmlDecode(content).Trim();
        }

        private static string? CleanText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = WebUtility.HtmlDecode(TagPattern.Replace(value, " "));
            // Double-encoded entities (&amp;amp;) show up on a lot of WordPress recipe plugins.
            text = WebUtility.HtmlDecode(text);
            text = WhitespacePattern.Replace(text, " ").Trim();
            return text.Length == 0 ? null : text;
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd() + "…";

        private static bool IsHost(Uri uri, string[] hosts) =>
            hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

        private class TikTokOEmbed
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("author_name")]
            public string? AuthorName { get; set; }

            [JsonPropertyName("thumbnail_url")]
            public string? ThumbnailUrl { get; set; }
        }
    }
}
