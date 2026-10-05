using System.Net;
using System.Text.RegularExpressions;

namespace My_Recipe_Keeper.Core.Services
{
    public class ParsedRecipeText
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string> Ingredients { get; } = new();
        public List<string> Instructions { get; } = new();
        public List<string> Notes { get; } = new();

        /// <summary>True when "Ingredients:"/"Method:" style headings structured the text.</summary>
        public bool UsedHeadings { get; set; }

        public bool HasRecipe => Ingredients.Count > 0;
    }

    /// <summary>
    /// Free-text recipe parser for social captions (Instagram/TikTok/Facebook), where there's no
    /// structured data — just a creator's caption with "Ingredients:" / "Method:" style headings,
    /// emoji bullets and a hashtag block. Pure string work, no network, so it's fully unit tested.
    /// </summary>
    public static class RecipeTextParser
    {
        private enum Section
        {
            Intro,
            Ingredients,
            Instructions,
            Notes
        }

        // Instagram og:description wraps the caption: `1,234 likes, 56 comments - chef on May 1, 2025: "caption"`.
        // Accounts that hide like counts drop the counts prefix: `chef on May 1, 2025: "caption"`.
        private static readonly Regex InstagramWrapper = new(
            @"^\s*(?:[\d,.]+[KkMm]?\s+likes?,\s*[\d,.]+[KkMm]?\s+comments?\s*-\s*)?[\w.]+\s+on\s+[A-Z][a-z]+\s+\d{1,2},\s+\d{4}\s*:\s*[""“](?<caption>.*)[""”]\s*\.?\s*$",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // og:title variant: `Chef Name on Instagram: "caption"`
        private static readonly Regex OnPlatformWrapper = new(
            @"^\s*.+?\s+on\s+(Instagram|TikTok|Facebook|Threads)\s*:\s*[""“](?<caption>.*)[""”]\s*\.?\s*$",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Leading emoji/symbols/punctuation, so "🛒 INGREDIENTS:" and "** Method **" match the heading rules.
        private static readonly Regex LeadingNonWord = new(@"^[^\p{L}\p{N}]+", RegexOptions.Compiled);

        private static readonly Regex IngredientsHeading = new(
            @"^(ingredients?|ingredient list|materials|you('ll| will)? need|what you('ll| will)? need|shopping list|for (the )?[\p{L} ]{1,30})\b[^\p{L}\p{N}]*(?<rest>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex InstructionsHeading = new(
            @"^(method|instructions?|directions?|steps?|how to make( it| this)?|preparation|to make)\b[^\p{L}\p{N}]*(?<rest>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NotesHeading = new(
            @"^(notes?|tips?|top tips?|chef'?s tips?)\b[^\p{L}\p{N}]*(?<rest>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Bullet = new(@"^\s*([-*•·▪▫►▶➤➡✔✓✅☑▢◦○●–—~>]|🔸|🔹|🔺|🟢|⭐|✨|👉)+\s*", RegexOptions.Compiled);

        private static readonly Regex StepNumber = new(@"^\s*(step\s*)?\d{1,2}\s*[.):\-]\s*|^\s*step\s*\d{1,2}\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Keycap digit emoji: 1️⃣ 2️⃣ ...
        private static readonly Regex KeycapNumber = new(@"^\s*\d️?⃣\s*", RegexOptions.Compiled);

        private static readonly Regex QuantityStart = new(
            @"^(\d|[½⅓⅔¼¾⅕⅛]|a |an |one |two |three |four |half |pinch |handful |dash |splash )",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex InlineListSeparator = new(@"\s*[,;]\s*", RegexOptions.Compiled);

        public static string UnwrapSocialCaption(string text)
        {
            var decoded = WebUtility.HtmlDecode(text);
            var match = InstagramWrapper.Match(decoded);
            if (!match.Success)
            {
                match = OnPlatformWrapper.Match(decoded);
            }

            return match.Success ? match.Groups["caption"].Value : decoded;
        }

        public static ParsedRecipeText Parse(string? text)
        {
            var result = new ParsedRecipeText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var caption = UnwrapSocialCaption(text).Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = caption.Split('\n').Select(l => l.Trim()).ToList();

            var section = Section.Intro;
            var sawHeading = false;
            var intro = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = CaptionCleaner.CleanLine(rawLine);
                if (line is null)
                {
                    continue;
                }

                var headingText = LeadingNonWord.Replace(line, string.Empty);

                if (TryHeading(IngredientsHeading, headingText, out var rest, out var headingLabel))
                {
                    sawHeading = true;
                    section = Section.Ingredients;
                    // "For the sauce:" is a sub-group of ingredients — keep it as a group label.
                    if (headingLabel.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Ingredients.Add(Capitalise(headingLabel.TrimEnd(':')) + ":");
                    }

                    AddInlineIngredients(result, rest);
                    continue;
                }

                if (TryHeading(InstructionsHeading, headingText, out rest, out _))
                {
                    sawHeading = true;
                    section = Section.Instructions;
                    AddIfNotEmpty(result.Instructions, CleanStep(rest));
                    continue;
                }

                if (TryHeading(NotesHeading, headingText, out rest, out _))
                {
                    sawHeading = true;
                    section = Section.Notes;
                    AddIfNotEmpty(result.Notes, CleanBullet(rest));
                    continue;
                }

                switch (section)
                {
                    case Section.Ingredients:
                        AddIfNotEmpty(result.Ingredients, CleanBullet(line));
                        break;
                    case Section.Instructions:
                        AddIfNotEmpty(result.Instructions, CleanStep(line));
                        break;
                    case Section.Notes:
                        AddIfNotEmpty(result.Notes, CleanBullet(line));
                        break;
                    default:
                        intro.Add(line);
                        break;
                }
            }

            if (!sawHeading)
            {
                ClassifyWithoutHeadings(intro, result);
                return result;
            }

            result.UsedHeadings = true;
            ApplyIntro(intro, result);
            return result;
        }

        private static bool TryHeading(Regex heading, string line, out string rest, out string label)
        {
            rest = string.Empty;
            label = string.Empty;

            // A heading is short ("Ingredients:", "Method 👇") or explicitly followed by a colon
            // with content ("Ingredients: 2 eggs, flour"). Stops "Steps up the flavour..." matching.
            var match = heading.Match(line);
            if (!match.Success)
            {
                return false;
            }

            rest = match.Groups["rest"].Value.Trim();
            label = line[..(line.Length - match.Groups["rest"].Value.Length)].Trim();
            var hasColon = label.EndsWith(':') || label.Contains(':');

            // "For …" group headings must carry a colon, else "For dinner tonight" would start a section.
            if (label.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
            {
                return hasColon;
            }

            return rest.Length == 0 || hasColon;
        }

        private static void AddInlineIngredients(ParsedRecipeText result, string rest)
        {
            if (rest.Length == 0)
            {
                return;
            }

            foreach (var item in InlineListSeparator.Split(rest))
            {
                AddIfNotEmpty(result.Ingredients, CleanBullet(item));
            }
        }

        /// <summary>
        /// No headings at all: quantity-led or bulleted short lines are ingredients, numbered lines
        /// are steps, the first plain line is the title and the rest is description.
        /// </summary>
        private static void ClassifyWithoutHeadings(List<string> lines, ParsedRecipeText result)
        {
            var leftover = new List<string>();
            var ingredientLines = new HashSet<string>();

            foreach (var line in lines)
            {
                if (StepNumber.IsMatch(line) || KeycapNumber.IsMatch(line))
                {
                    AddIfNotEmpty(result.Instructions, CleanStep(line));
                }
                else if ((Bullet.IsMatch(line) || QuantityStart.IsMatch(line)) && line.Length <= 90)
                {
                    AddIfNotEmpty(result.Ingredients, CleanBullet(line));
                    ingredientLines.Add(line);
                }
                else
                {
                    leftover.Add(line);
                }
            }

            // A single quantity-looking line isn't a recipe (e.g. "3 ways to use leftovers!"). Put it back
            // in its original position so the first line of the caption still becomes the title.
            if (result.Ingredients.Count < 2)
            {
                leftover = lines.Where(l => ingredientLines.Contains(l) || leftover.Contains(l)).ToList();
                result.Ingredients.Clear();
            }

            ApplyIntro(leftover, result);
        }

        private static void ApplyIntro(List<string> intro, ParsedRecipeText result)
        {
            if (intro.Count == 0)
            {
                return;
            }

            var title = TrimTitle(intro[0]);
            var usedWholeLine = title.Length is > 0 and <= 100;

            // Long first line ("CHINESE BBQ PORK - Sticky, flavorful char siu…"): the name is usually the
            // part before the first dash/colon/sentence end. The full line then stays in the description.
            if (!usedWholeLine)
            {
                var head = TitleBreak.Split(title, 2)[0];
                title = head.Length is >= 3 and <= 80 ? TrimTitle(head) : string.Empty;
            }

            result.Title = title.Length > 0 ? title : null;

            var descriptionLines = usedWholeLine ? intro.Skip(1) : intro;
            var description = string.Join("\n", descriptionLines).Trim();
            result.Description = description.Length > 0 ? description : null;
        }

        private static readonly Regex TitleBreak = new(@"\s+[-–—|]\s+|:\s+|[.!?]\s+", RegexOptions.Compiled);

        private static readonly Regex SocialTitlePrefix = new(
            @"^\s*.+?\s+on\s+(Instagram|TikTok|Facebook|Threads)\s*:\s*[""“]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>og:title of a social post ("Chef on Instagram: …"), even when Instagram truncated it.</summary>
        public static bool IsSocialPostTitle(string? pageTitle) =>
            pageTitle is not null && SocialTitlePrefix.IsMatch(System.Net.WebUtility.HtmlDecode(pageTitle));

        /// <summary>"Chef on Instagram: \"Tacos…" → "Tacos…".</summary>
        public static string StripSocialPostTitle(string pageTitle) =>
            SocialTitlePrefix.Replace(System.Net.WebUtility.HtmlDecode(pageTitle), string.Empty).TrimEnd('"', '”', ' ', '.');

        private static string TrimTitle(string line)
        {
            var title = LeadingNonWord.Replace(line, string.Empty).Trim();
            return Regex.Replace(title, @"[^\p{L}\p{N})!?.'’&]+$", string.Empty).Trim();
        }

        private static string CleanBullet(string line)
        {
            var cleaned = KeycapNumber.Replace(line, string.Empty);
            cleaned = Bullet.Replace(cleaned, string.Empty);
            return cleaned.Trim();
        }

        private static string CleanStep(string line)
        {
            var cleaned = KeycapNumber.Replace(line, string.Empty);
            cleaned = Bullet.Replace(cleaned, string.Empty);
            cleaned = StepNumber.Replace(cleaned, string.Empty);
            return cleaned.Trim();
        }

        private static void AddIfNotEmpty(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value);
            }
        }

        private static string Capitalise(string value) =>
            value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
