using System.Text;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    public class RecipeShareFormatter : IRecipeShareFormatter
    {
        public string Format(RecipeEntity recipe, ShareFormat format, IReadOnlyCollection<string>? excludedIngredients = null, bool includeSourceLink = true)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            var text = format == ShareFormat.ShoppingList
                ? FormatShoppingList(recipe, excludedIngredients)
                : FormatFullRecipe(recipe);

            if (includeSourceLink && !string.IsNullOrWhiteSpace(recipe.SourceUrl))
            {
                text.AppendLine().Append("🔗 ").AppendLine(recipe.SourceUrl);
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>Group labels ("For the sauce:") are stored as lines ending in a colon.</summary>
        public static bool IsGroupLabel(string line) => line.EndsWith(':');

        private static StringBuilder FormatShoppingList(RecipeEntity recipe, IReadOnlyCollection<string>? excluded)
        {
            var skip = excluded is null
                ? new HashSet<string>()
                : new HashSet<string>(excluded.Select(e => e.Trim()), StringComparer.OrdinalIgnoreCase);

            var text = new StringBuilder();
            text.Append("🛒 *Shopping list: ").Append(recipe.Title).AppendLine("*");
            if (!string.IsNullOrWhiteSpace(recipe.Servings))
            {
                text.Append("_").Append(recipe.Servings).AppendLine("_");
            }

            text.AppendLine();

            // Drop group labels that end up with nothing under them once "already have" items are removed.
            string? pendingLabel = null;
            var any = false;
            foreach (var line in recipe.Ingredients)
            {
                if (IsGroupLabel(line))
                {
                    pendingLabel = line;
                    continue;
                }

                if (skip.Contains(line))
                {
                    continue;
                }

                if (pendingLabel is not null)
                {
                    text.AppendLine().Append('*').Append(pendingLabel.TrimEnd(':')).AppendLine("*");
                    pendingLabel = null;
                }

                text.Append("▢ ").AppendLine(line);
                any = true;
            }

            if (!any)
            {
                text.AppendLine("Nothing to buy — you've got everything! 🎉");
            }

            return text;
        }

        private static StringBuilder FormatFullRecipe(RecipeEntity recipe)
        {
            var text = new StringBuilder();
            text.Append("🍽️ *").Append(recipe.Title).AppendLine("*");

            if (!string.IsNullOrWhiteSpace(recipe.Description))
            {
                text.Append('_').Append(recipe.Description.Trim()).AppendLine("_");
            }

            var facts = new List<string>();
            if (!string.IsNullOrWhiteSpace(recipe.PrepTime))
            {
                facts.Add($"Prep {recipe.PrepTime}");
            }

            if (!string.IsNullOrWhiteSpace(recipe.CookTime))
            {
                facts.Add($"Cook {recipe.CookTime}");
            }

            if (facts.Count == 0 && !string.IsNullOrWhiteSpace(recipe.TotalTime))
            {
                facts.Add($"Total {recipe.TotalTime}");
            }

            if (!string.IsNullOrWhiteSpace(recipe.Servings))
            {
                facts.Add($"Serves {recipe.Servings}");
            }

            if (facts.Count > 0)
            {
                text.AppendLine().Append("⏱️ ").AppendLine(string.Join(" · ", facts));
            }

            if (recipe.Ingredients.Count > 0)
            {
                text.AppendLine().AppendLine("🥕 *Ingredients*");
                foreach (var line in recipe.Ingredients)
                {
                    if (IsGroupLabel(line))
                    {
                        text.Append('_').Append(line.TrimEnd(':')).AppendLine("_");
                    }
                    else
                    {
                        text.Append("• ").AppendLine(line);
                    }
                }
            }

            if (recipe.Instructions.Count > 0)
            {
                text.AppendLine().AppendLine("👩‍🍳 *Method*");
                var step = 1;
                foreach (var line in recipe.Instructions)
                {
                    if (IsGroupLabel(line))
                    {
                        text.Append('_').Append(line.TrimEnd(':')).AppendLine("_");
                    }
                    else
                    {
                        text.Append(step++).Append(". ").AppendLine(line);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(recipe.Notes))
            {
                text.AppendLine().AppendLine("📝 *Notes*").AppendLine(recipe.Notes.Trim());
            }

            return text;
        }
    }
}
