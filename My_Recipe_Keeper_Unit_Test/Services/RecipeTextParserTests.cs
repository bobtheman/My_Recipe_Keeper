using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class RecipeTextParserTests
    {
        [Fact]
        public void Parse_CaptionWithHeadings_SplitsIngredientsAndSteps()
        {
            const string caption = """
                Easy Garlic Butter Pasta 🍝
                Ready in 15 minutes!

                🛒 INGREDIENTS:
                - 200g spaghetti
                - 3 cloves garlic
                • 50g butter

                👩‍🍳 Method:
                1. Boil the pasta.
                2) Melt the butter with garlic.
                Step 3: Toss together.

                #pasta #easyrecipes #dinner
                """;

            var result = RecipeTextParser.Parse(caption);

            Assert.Equal("Easy Garlic Butter Pasta", result.Title);
            Assert.Equal("Ready in 15 minutes!", result.Description);
            Assert.Equal(new[] { "200g spaghetti", "3 cloves garlic", "50g butter" }, result.Ingredients);
            Assert.Equal(new[] { "Boil the pasta.", "Melt the butter with garlic.", "Toss together." }, result.Instructions);
        }

        [Fact]
        public void Parse_InstagramOgDescriptionWrapper_IsStripped()
        {
            const string ogDescription = "1,234 likes, 56 comments - chefbob on March 1, 2025: \"Brownies\nIngredients:\n2 eggs\n100g chocolate\nMethod:\nMix and bake\".";

            var result = RecipeTextParser.Parse(ogDescription);

            Assert.Equal("Brownies", result.Title);
            Assert.Equal(new[] { "2 eggs", "100g chocolate" }, result.Ingredients);
            Assert.Equal(new[] { "Mix and bake" }, result.Instructions);
        }

        [Fact]
        public void Parse_InstagramWrapperWithHiddenLikes_IsStripped()
        {
            // Real shape seen live (sup3rsami reel): no "likes, comments -" prefix when counts are hidden.
            const string ogDescription = "sup3rsami on July 8, 2024: \"Scallop Pasta 🍝\nIngredients:\n1 lb Scallops\n1/4 cup diced parsley\n#pasta #dinner\".";

            var result = RecipeTextParser.Parse(ogDescription);

            Assert.Equal("Scallop Pasta", result.Title);
            Assert.Equal(new[] { "1 lb Scallops", "1/4 cup diced parsley" }, result.Ingredients);
        }

        [Fact]
        public void Parse_InlineIngredientsAfterHeading_SplitOnCommas()
        {
            var result = RecipeTextParser.Parse("Smoothie\nIngredients: 1 banana, 200ml milk, 1 tbsp honey\nMethod: Blend.");

            Assert.Equal(new[] { "1 banana", "200ml milk", "1 tbsp honey" }, result.Ingredients);
            Assert.Equal(new[] { "Blend." }, result.Instructions);
        }

        [Fact]
        public void Parse_ForTheGroupHeading_KeptAsGroupLabel()
        {
            var result = RecipeTextParser.Parse("Tacos\nIngredients:\n500g beef\nFor the salsa:\n2 tomatoes\nMethod:\nCook.");

            Assert.Equal(new[] { "500g beef", "For the salsa:", "2 tomatoes" }, result.Ingredients);
        }

        [Fact]
        public void Parse_NoHeadings_ClassifiesByQuantityAndNumbering()
        {
            const string caption = """
                Quick Omelette
                2 eggs
                1 tbsp butter
                Pinch of salt
                1. Whisk the eggs
                2. Fry in butter
                """;

            var result = RecipeTextParser.Parse(caption);

            Assert.Equal("Quick Omelette", result.Title);
            Assert.Equal(3, result.Ingredients.Count);
            Assert.Equal(new[] { "Whisk the eggs", "Fry in butter" }, result.Instructions);
        }

        [Fact]
        public void Parse_NoRecipeContent_HasRecipeFalse()
        {
            var result = RecipeTextParser.Parse("What a lovely day at the beach! #sun #holiday");

            Assert.False(result.HasRecipe);
            Assert.Equal("What a lovely day at the beach!", result.Title);
        }

        [Fact]
        public void Parse_SentenceStartingWithHeadingWord_NotTreatedAsHeading()
        {
            var result = RecipeTextParser.Parse("Soup\nIngredients:\n1 onion\nSteps up any weeknight dinner\nMethod:\nSimmer.");

            Assert.Contains("Steps up any weeknight dinner", result.Ingredients);
            Assert.Equal(new[] { "Simmer." }, result.Instructions);
        }

        [Fact]
        public void Parse_NotesSection_Captured()
        {
            var result = RecipeTextParser.Parse("Cake\nIngredients:\n2 eggs\nMethod:\nBake\nTips:\nFreezes well");

            Assert.Equal(new[] { "Freezes well" }, result.Notes);
        }

        [Fact]
        public void Parse_EmptyInput_ReturnsEmptyResult()
        {
            var result = RecipeTextParser.Parse("   ");

            Assert.False(result.HasRecipe);
            Assert.Null(result.Title);
        }
    }
}
