using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Interfaces
{
    public class RecipePage
    {
        public List<RecipeEntity> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public interface IRecipeRepository
    {
        Task InitializeAsync();

        /// <summary>Closes the underlying connection so the db file can be copied/replaced (backup/restore).</summary>
        Task CloseAsync();

        Task ReopenAsync();

        Task<RecipePage> GetPageAsync(int page, int pageSize, string? searchText = null, bool favoriteOnly = false);

        Task<RecipeEntity?> GetByIdAsync(int id);

        Task<RecipeEntity?> GetBySourceUrlAsync(string sourceUrl);

        Task<int> SaveAsync(RecipeEntity recipe);

        Task DeleteAsync(int id);

        Task<int> GetCountAsync();
    }
}
