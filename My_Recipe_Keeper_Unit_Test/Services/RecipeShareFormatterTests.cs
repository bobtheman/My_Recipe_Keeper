using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class RecipeShareFormatterTests
    {
        private readonly RecipeShareFormatter _sut = new();

        private static RecipeEntity Sample() => new()
        {
            Title = "Tacos",
            Servings = "4 servings",
            PrepTime = "10 mins",
            CookTime = "20 mins",
            IngredientsText = "500g beef\nFor the salsa:\n2 tomatoes\n1 onion",
            InstructionsText = "Brown the beef\nChop the salsa",
            SourceUrl = "https://example.com/tacos"
        };

        [Fact]
        public void Format_ShoppingList_ListsIngredientsWithTickBoxesAndBoldTitle()
        {
            var text = _sut.Format(Sample(), ShareFormat.ShoppingList);

            Assert.StartsWith("🛒 *Shopping list: Tacos*", text);
            Assert.Contains("▢ 500g beef", text);
            Assert.Contains("*For the salsa*", text);
            Assert.DoesNotContain("Brown the beef", text);
            Assert.EndsWith("🔗 https://example.com/tacos", text);
        }

        [Fact]
        public void Format_ShoppingList_ExcludesAlreadyHaveItemsAndEmptyGroups()
        {
            var text = _sut.Format(Sample(), ShareFormat.ShoppingList, new[] { "2 tomatoes", "1 onion" });

            Assert.Contains("▢ 500g beef", text);
            Assert.DoesNotContain("tomatoes", text);
            Assert.DoesNotContain("For the salsa", text);
        }

        [Fact]
        public void Format_ShoppingList_EverythingExcluded_SaysNothingToBuy()
        {
            var text = _sut.Format(Sample(), ShareFormat.ShoppingList, new[] { "500g beef", "2 tomatoes", "1 onion" });

            Assert.Contains("Nothing to buy", text);
        }

        [Fact]
        public void Format_FullRecipe_NumbersStepsAndShowsFacts()
        {
            var text = _sut.Format(Sample(), ShareFormat.FullRecipe);

            Assert.Contains("🍽️ *Tacos*", text);
            Assert.Contains("Prep 10 mins · Cook 20 mins · Serves 4 servings", text);
            Assert.Contains("• 500g beef", text);
            Assert.Contains("_For the salsa_", text);
            Assert.Contains("1. Brown the beef", text);
            Assert.Contains("2. Chop the salsa", text);
        }

        [Fact]
        public void Format_IncludeSourceLinkFalse_OmitsLink()
        {
            var text = _sut.Format(Sample(), ShareFormat.FullRecipe, includeSourceLink: false);

            Assert.DoesNotContain("example.com", text);
        }
    }
}
