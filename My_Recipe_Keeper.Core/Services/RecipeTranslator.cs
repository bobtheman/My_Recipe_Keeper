using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    public class RecipeTranslator : IRecipeTranslator
    {
        private readonly ITranslationService _translationService;

        public RecipeTranslator(ITranslationService translationService)
        {
            _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        }

        public async Task<string?> TranslateAsync(RecipeEntity recipe, string targetLanguage, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            // Ingredients first: the longest field gives the most reliable language detection, and if it's
            // already in the target language the other fields are skipped entirely.
            var fields = new (Func<string?> Get, Action<string> Set)[]
            {
                (() => recipe.IngredientsText, v => recipe.IngredientsText = v),
                (() => recipe.InstructionsText, v => recipe.InstructionsText = v),
                (() => recipe.Title, v => recipe.Title = v),
                (() => recipe.Description, v => recipe.Description = v),
                (() => recipe.Notes, v => recipe.Notes = v),
                (() => recipe.Category, v => recipe.Category = v),
                (() => recipe.Servings, v => recipe.Servings = v)
            };

            string? source = null;
            foreach (var (get, set) in fields)
            {
                var value = get();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var result = await _translationService.TranslateAsync(value, targetLanguage, ct);
                if (result is null)
                {
                    // Service unreachable: stop, leaving any fields already translated as they are.
                    return source;
                }

                if (source is null && string.Equals(result.SourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                source ??= result.SourceLanguage;
                set(result.Text.Trim());
            }

            return source;
        }
    }
}
