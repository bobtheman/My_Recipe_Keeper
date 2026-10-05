using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class SqliteRecipeRepositoryTests : IDisposable
    {
        private readonly FakeAppPaths _paths = new();
        private readonly SqliteRecipeRepository _sut;

        public SqliteRecipeRepositoryTests()
        {
            _sut = new SqliteRecipeRepository(_paths);
        }

        public void Dispose() => _paths.Dispose();

        [Fact]
        public async Task SaveAsync_NewRecipe_AssignsIdAndPersists()
        {
            var recipe = new RecipeEntity { Title = "Chicken Tikka", IngredientsText = "500g chicken\n1 onion" };

            var id = await _sut.SaveAsync(recipe);

            Assert.True(id > 0);
            var saved = await _sut.GetByIdAsync(id);
            Assert.NotNull(saved);
            Assert.Equal("Chicken Tikka", saved!.Title);
            Assert.Equal(new[] { "500g chicken", "1 onion" }, saved.Ingredients);
        }

        [Fact]
        public async Task SaveAsync_ExistingRecipe_UpdatesInPlace()
        {
            var recipe = new RecipeEntity { Title = "Pancakes" };
            var id = await _sut.SaveAsync(recipe);

            recipe.Servings = "4 servings";
            await _sut.SaveAsync(recipe);

            var updated = await _sut.GetByIdAsync(id);
            Assert.Equal("4 servings", updated!.Servings);
            Assert.Equal(1, await _sut.GetCountAsync());
        }

        [Fact]
        public async Task DeleteAsync_RemovesRecipe()
        {
            var id = await _sut.SaveAsync(new RecipeEntity { Title = "Soup" });

            await _sut.DeleteAsync(id);

            Assert.Null(await _sut.GetByIdAsync(id));
            Assert.Equal(0, await _sut.GetCountAsync());
        }

        [Fact]
        public async Task GetPageAsync_SearchMatchesTitleCaseInsensitive()
        {
            await _sut.SaveAsync(new RecipeEntity { Title = "Banana Bread" });
            await _sut.SaveAsync(new RecipeEntity { Title = "Lasagne" });

            var page = await _sut.GetPageAsync(page: 1, pageSize: 20, searchText: "BANANA");

            var match = Assert.Single(page.Items);
            Assert.Equal("Banana Bread", match.Title);
        }

        [Fact]
        public async Task GetPageAsync_SearchMatchesIngredients()
        {
            await _sut.SaveAsync(new RecipeEntity { Title = "Curry", IngredientsText = "2 chicken breasts\n1 tin tomatoes" });
            await _sut.SaveAsync(new RecipeEntity { Title = "Salad", IngredientsText = "lettuce" });

            var page = await _sut.GetPageAsync(page: 1, pageSize: 20, searchText: "chicken");

            Assert.Equal("Curry", Assert.Single(page.Items).Title);
        }

        [Fact]
        public async Task GetPageAsync_FavoriteOnly_ExcludesOthers()
        {
            await _sut.SaveAsync(new RecipeEntity { Title = "Fave", IsFavorite = true });
            await _sut.SaveAsync(new RecipeEntity { Title = "Meh" });

            var page = await _sut.GetPageAsync(page: 1, pageSize: 20, favoriteOnly: true);

            Assert.Equal("Fave", Assert.Single(page.Items).Title);
        }

        [Fact]
        public async Task GetPageAsync_RespectsPageSizeAndReportsTotalCount()
        {
            for (var i = 0; i < 5; i++)
            {
                await _sut.SaveAsync(new RecipeEntity { Title = $"Recipe {i}" });
            }

            var page = await _sut.GetPageAsync(page: 1, pageSize: 2);

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(5, page.TotalCount);
        }

        [Fact]
        public async Task GetBySourceUrlAsync_FindsExistingImport()
        {
            await _sut.SaveAsync(new RecipeEntity { Title = "Imported", SourceUrl = "https://example.com/r/1" });

            var found = await _sut.GetBySourceUrlAsync("https://example.com/r/1");

            Assert.NotNull(found);
            Assert.Null(await _sut.GetBySourceUrlAsync("https://example.com/r/2"));
        }

        [Fact]
        public async Task CloseThenReopen_DataSurvives()
        {
            var id = await _sut.SaveAsync(new RecipeEntity { Title = "Risotto" });

            await _sut.CloseAsync();
            await _sut.ReopenAsync();

            var recipe = await _sut.GetByIdAsync(id);
            Assert.NotNull(recipe);
            Assert.Equal("Risotto", recipe!.Title);
        }
    }
}
