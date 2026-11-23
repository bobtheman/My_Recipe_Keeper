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

        private RecipeEntity _recipe = new();
        private bool _translating;
        private string _importText = string.Empty;
        private bool _importing;
        private bool _saving;
        private bool _notFound;
        private string? _banner;
        private string _bannerClass = "banner-ok";
        private string? _lastAutoImported;

        private bool _showForm;

        private bool IsNew => Id is null;

        // New recipe: the form stays hidden until an import result arrives or the user chooses to type their own.
        private bool ShowForm => !IsNew || _showForm;

        private void StartFromScratch() => _showForm = true;

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
                _showForm = false;
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
                    SetBanner($"😕 {result.Error} You can still enter it yourself below.", "banner-error");
                    return;
                }

                var scraped = result.Recipe!;
                _importText = scraped.SourceUrl ?? _importText;

                var duplicate = scraped.SourceUrl is null ? null : await RecipeRepository.GetBySourceUrlAsync(scraped.SourceUrl);

                // Keep anything the user already typed; scraped values fill the gaps.
                _recipe = Merge(_recipe, scraped);
                _showForm = true;

                var message = result.Source switch
                {
                    ScrapeSource.StructuredData => "🎉 Got it! Give it a quick check, then save.",
                    ScrapeSource.SocialCaption => "🎉 Pulled this from the post caption — captions can be messy, so give it a once-over.",
                    _ => "🤏 Got the title and photo, but the caption has no recipe — it's probably shown in the video. Add the ingredients and steps below."
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
