using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper
{
    /// <summary>Emoji "cover art" for recipes without a photo — picked by keyword, else stable per recipe.</summary>
    public static class RecipeVisuals
    {
        private static readonly (string Keyword, string Emoji)[] Keywords =
        {
            ("pasta", "🍝"), ("spaghetti", "🍝"), ("lasagne", "🍝"), ("lasagna", "🍝"),
            ("pizza", "🍕"), ("burger", "🍔"), ("taco", "🌮"), ("burrito", "🌯"),
            ("curry", "🍛"), ("rice", "🍚"), ("noodle", "🍜"), ("ramen", "🍜"), ("soup", "🍲"), ("stew", "🍲"),
            ("salad", "🥗"), ("sushi", "🍣"), ("chicken", "🍗"), ("steak", "🥩"), ("beef", "🥩"),
            ("fish", "🐟"), ("salmon", "🐟"), ("prawn", "🍤"), ("shrimp", "🍤"),
            ("pancake", "🥞"), ("waffle", "🧇"), ("egg", "🍳"), ("omelette", "🍳"), ("breakfast", "🍳"),
            ("bread", "🍞"), ("sandwich", "🥪"), ("cake", "🍰"), ("cookie", "🍪"), ("brownie", "🍫"),
            ("chocolate", "🍫"), ("pie", "🥧"), ("cupcake", "🧁"), ("muffin", "🧁"), ("ice cream", "🍨"),
            ("smoothie", "🥤"), ("cocktail", "🍹"), ("potato", "🥔"), ("chips", "🍟"), ("fries", "🍟"),
            ("dumpling", "🥟"), ("veg", "🥦"), ("avocado", "🥑"), ("cheese", "🧀")
        };

        private static readonly string[] Fallbacks = { "🍲", "🥘", "🍝", "🥗", "🍛", "🥙", "🍜", "🥧", "🍱", "🍳" };

        public static string EmojiFor(RecipeEntity recipe)
        {
            var title = recipe.Title.ToLowerInvariant();
            foreach (var (keyword, emoji) in Keywords)
            {
                if (title.Contains(keyword))
                {
                    return emoji;
                }
            }

            var index = Math.Abs(recipe.Id == 0 ? title.Length : recipe.Id) % Fallbacks.Length;
            return Fallbacks[index];
        }

        public static string PlaceholderBackground(RecipeEntity recipe)
        {
            string[] colors = { "#FFE1CC", "#FFF1C2", "#DDF3E4", "#FFD9E4", "#E4E8FF" };
            return colors[Math.Abs(recipe.Title.Length + recipe.Id) % colors.Length];
        }
    }
}
