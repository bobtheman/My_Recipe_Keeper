using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Components.Pages
{
    public partial class RecipeEditor
    {
        [Parameter]
        public int? Id { get; set; }

        /// <summary>Text from the Android share sheet (MainLayout routes it here) — imported automatically.</summary>
        [SupplyParameterFromQuery(Name = "shared")]
        public string? Shared { get; set; }

        /// <summary>Set by MainLayout when screenshots/recordings were shared in; the media itself waits in the inbox.</summary>
        [SupplyParameterFromQuery(Name = "media")]
        public string? Media { get; set; }

        private RecipeEntity _recipe = new();
        private string? _ocrText;
        private bool _ocrBusy;
        private string _ocrStatus = string.Empty;
        private string? _lastMediaNonce;
        private bool _translating;
        private string _importText = string.Empty;
        private bool _importing;
        private bool _saving;
        private bool _notFound;
        private string? _banner;
        private string _bannerClass = "banner-ok";
        private string? _lastAutoImported;

        private bool IsNew => Id is null;

        protected override async Task OnParametersSetAsync()
        {
            if (Id is int id)
            {
                var existing = await RecipeRepository.GetByIdAsync(id);
                _notFound = existing is null;
                _recipe = existing ?? new RecipeEntity();
                return;
            }

            // Same component instance is reused when navigating edit → add; don't carry the old recipe over.
            if (_recipe.Id != 0)
            {
                _recipe = new RecipeEntity();
                _notFound = false;
                _banner = null;
            }

            // A second share while this page is already open changes only the query string, so this
            // runs again — import each distinct shared payload once.
            if (!string.IsNullOrWhiteSpace(Shared) && Shared != _lastAutoImported)
            {
                _lastAutoImported = Shared;
                _recipe = new RecipeEntity();
                _importText = Shared;
                await ImportAsync();
            }

            if (!string.IsNullOrWhiteSpace(Media) && Media != _lastMediaNonce)
            {
                _lastMediaNonce = Media;
                var paths = SharedLinkInbox.TakeMedia();
                if (paths.Count > 0)
                {
                    await ReadMediaAsync(paths);
                }
            }
        }

        private async Task PickMediaAsync()
        {
            IReadOnlyList<string> paths;
            try
            {
                paths = await ImagePicker.PickMediaAsync();
            }
            catch (Exception ex)
            {
                SetBanner($"Couldn't open your gallery: {ex.Message}", "banner-error");
                return;
            }

            if (paths.Count > 0)
            {
                await ReadMediaAsync(paths);
            }
        }

        private async Task ReadMediaAsync(IReadOnlyList<string> paths)
        {
            _ocrBusy = true;
            _ocrStatus = "Getting ready…";
            _banner = null;
            StateHasChanged();

            var progress = new Progress<string>(status =>
            {
                _ocrStatus = status;
                _ = InvokeAsync(StateHasChanged);
            });

            try
            {
                var blocks = await MediaTextService.ReadTextBlocksAsync(paths, progress);
                if (blocks.Count == 0)
                {
                    SetBanner("😕 Couldn't spot any text in those. Try screenshots where the recipe text is showing.", "banner-error");
                    return;
                }

                _ocrText = string.Join("\n\n", blocks);
            }
            catch (Exception ex)
            {
                SetBanner($"Couldn't read that: {ex.Message}", "banner-error");
            }
            finally
            {
                _ocrBusy = false;
                StateHasChanged();
            }
        }

        private void DiscardOcr() => _ocrText = null;

        private async Task AddOcrToRecipeAsync()
        {
            // Translate the reviewed OCR text before parsing, same as captions: English headings/units parse best.
            var text = _ocrText ?? string.Empty;
            string? translatedFrom = null;
            var target = await TranslateTargetAsync();
            if (target is not null && text.Trim().Length > 0)
            {
                var translated = await TranslationService.TranslateAsync(text, target);
                if (translated is not null && !string.Equals(translated.SourceLanguage, target, StringComparison.OrdinalIgnoreCase))
                {
                    text = translated.Text;
                    translatedFrom = translated.SourceLanguage;
                }
            }

            var parsed = OverlayTextParser.Parse(text);
            if (parsed.Ingredients.Count == 0 && parsed.Instructions.Count == 0)
            {
                SetBanner("Nothing recipe-like left in that text.", "banner-warn");
                return;
            }

            // Append rather than replace: typically the link gave title/photo and this adds the rest,
            // and several batches of screenshots can be added one after another.
            _recipe.IngredientsText = AppendLines(_recipe.IngredientsText, parsed.Ingredients);
            _recipe.InstructionsText = AppendLines(_recipe.InstructionsText, parsed.Instructions);
            if (string.IsNullOrWhiteSpace(_recipe.Title) && parsed.Title is not null)
            {
                _recipe.Title = parsed.Title;
            }

            _ocrText = null;
            var translatedNote = translatedFrom is null ? string.Empty : $" 🌐 Translated from {LanguageName(translatedFrom)}.";
            SetBanner($"🎉 Added {parsed.Ingredients.Count} ingredients and {parsed.Instructions.Count} steps — tweak anything OCR got wrong, then save.{translatedNote}", "banner-ok");
        }

        private async Task TranslateRecipeAsync()
        {
            if (_translating)
            {
                return;
            }

            _translating = true;
            _banner = null;
            StateHasChanged();

            try
            {
                var source = await RecipeTranslator.TranslateAsync(_recipe, TranslationOptions.TargetLanguage);
                SetBanner(
                    source is null ? "👍 Already in English (or the translator couldn't be reached)." : $"🌐 Translated from {LanguageName(source)} — give it a quick check.",
                    source is null ? "banner-warn" : "banner-ok");
            }
            finally
            {
                _translating = false;
                StateHasChanged();
            }
        }

        private async Task<string?> TranslateTargetAsync() =>
            (await SettingsService.LoadAsync()).AutoTranslate ? TranslationOptions.TargetLanguage : null;

        private static string LanguageName(string code)
        {
            try
            {
                return System.Globalization.CultureInfo.GetCultureInfo(code).EnglishName;
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                return code;
            }
        }

        private static string AppendLines(string existing, IEnumerable<string> additions)
        {
            var current = existing.Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var seen = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            current.AddRange(additions.Where(seen.Add));
            return RecipeEntity.JoinLines(current);
        }

        private Task OnImportKeyDown(KeyboardEventArgs args) =>
            args.Key == "Enter" ? ImportAsync() : Task.CompletedTask;

        private async Task ImportAsync()
        {
            if (_importing || string.IsNullOrWhiteSpace(_importText))
            {
                return;
            }

            _importing = true;
            _banner = null;
            StateHasChanged();

            try
            {
                var result = await ScraperService.ScrapeAsync(_importText, await TranslateTargetAsync());
                if (!result.Success)
                {
                    SetBanner($"😕 {result.Error} You can still type it in below.", "banner-error");
                    return;
                }

                var scraped = result.Recipe!;
                _importText = scraped.SourceUrl ?? _importText;

                var duplicate = scraped.SourceUrl is null ? null : await RecipeRepository.GetBySourceUrlAsync(scraped.SourceUrl);

                // Keep anything the user already typed; scraped values fill the gaps.
                _recipe = Merge(_recipe, scraped);

                var message = result.Source switch
                {
                    ScrapeSource.StructuredData => "🎉 Got it! Give it a quick check, then save.",
                    ScrapeSource.SocialCaption => "🎉 Pulled this from the post caption — captions can be messy, so give it a once-over.",
                    _ => "🤏 Got the title and photo, but the caption has no recipe — it's probably shown in the video. Screen-record it and use 📸 below."
                };

                if (result.TranslatedFrom is not null)
                {
                    message += $" 🌐 Translated from {LanguageName(result.TranslatedFrom)}.";
                }

                if (duplicate is not null)
                {
                    message += $" (Heads up: you already saved this as \"{duplicate.Title}\".)";
                }

                SetBanner(message, result.Source == ScrapeSource.PageMetadata || duplicate is not null ? "banner-warn" : "banner-ok");
            }
            finally
            {
                _importing = false;
                StateHasChanged();
            }
        }

        private static RecipeEntity Merge(RecipeEntity current, RecipeEntity scraped)
        {
            static string? Pick(string? mine, string? theirs) => string.IsNullOrWhiteSpace(mine) ? theirs : mine;

            return new RecipeEntity
            {
                Id = current.Id,
                Title = Pick(current.Title, scraped.Title) ?? string.Empty,
                Description = Pick(current.Description, scraped.Description),
                IngredientsText = Pick(current.IngredientsText, scraped.IngredientsText) ?? string.Empty,
                InstructionsText = Pick(current.InstructionsText, scraped.InstructionsText) ?? string.Empty,
                Servings = Pick(current.Servings, scraped.Servings),
                PrepTime = Pick(current.PrepTime, scraped.PrepTime),
                CookTime = Pick(current.CookTime, scraped.CookTime),
                TotalTime = Pick(current.TotalTime, scraped.TotalTime),
                ImageUrl = Pick(current.ImageUrl, scraped.ImageUrl),
                SourceUrl = Pick(current.SourceUrl, scraped.SourceUrl),
                Category = Pick(current.Category, scraped.Category),
                Notes = Pick(current.Notes, scraped.Notes),
                IsFavorite = current.IsFavorite,
                DateAdded = current.DateAdded
            };
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_recipe.Title))
            {
                SetBanner("✋ Every recipe needs a name.", "banner-error");
                return;
            }

            _saving = true;
            try
            {
                _recipe.Title = _recipe.Title.Trim();
                // Normalise line endings/blank lines so the view and share text stay tidy.
                _recipe.IngredientsText = RecipeEntity.JoinLines(_recipe.IngredientsText.Replace("\r", string.Empty).Split('\n'));
                _recipe.InstructionsText = RecipeEntity.JoinLines(_recipe.InstructionsText.Replace("\r", string.Empty).Split('\n'));

                var id = await RecipeRepository.SaveAsync(_recipe);
                AutoBackup.TriggerIfEnabled();
                Nav.NavigateTo($"/recipe/{id}", replace: true);
            }
            catch (Exception ex)
            {
                SetBanner($"Couldn't save: {ex.Message}", "banner-error");
            }
            finally
            {
                _saving = false;
            }
        }

        private async Task DeleteAsync()
        {
            if (Id is not int id)
            {
                return;
            }

            var confirmed = await AlertService.ConfirmAsync("Delete recipe?", $"\"{_recipe.Title}\" will be removed from your cookbook.");
            if (!confirmed)
            {
                return;
            }

            await RecipeRepository.DeleteAsync(id);
            AutoBackup.TriggerIfEnabled();
            Nav.NavigateTo("/", replace: true);
        }

        private void GoBack() => Nav.NavigateTo(Id is int id ? $"/recipe/{id}" : "/");

        private void SetBanner(string message, string cssClass)
        {
            _banner = message;
            _bannerClass = cssClass;
        }
    }
}
