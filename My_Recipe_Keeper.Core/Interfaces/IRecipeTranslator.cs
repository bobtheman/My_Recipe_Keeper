using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IRecipeTranslator
    {
        /// <summary>
        /// Translates the recipe's text fields in place. Returns the detected source language when
        /// something was translated, or null if it was already in the target language or the service failed.
        /// </summary>
        Task<string?> TranslateAsync(RecipeEntity recipe, string targetLanguage, CancellationToken ct = default);
    }
}
