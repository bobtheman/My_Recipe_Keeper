using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IRecipeShareFormatter
    {
        /// <summary>
        /// Plain text using WhatsApp markdown (*bold*, _italic_) — still reads fine pasted into Notes/Keep.
        /// excludedIngredients: lines the user ticked as "already have", left off the shopping list.
        /// </summary>
        string Format(RecipeEntity recipe, ShareFormat format, IReadOnlyCollection<string>? excludedIngredients = null, bool includeSourceLink = true);
    }
}
