using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;
using SQLite;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Single SQLite connection for the whole app (registered as a singleton). InitializeAsync
    /// is idempotent and cheap after the first call, so pages can call it on every nav without
    /// a redundant round trip.
    /// </summary>
    public class SqliteRecipeRepository : IRecipeRepository
    {
        private readonly IAppPaths _paths;
        private SQLiteAsyncConnection? _db;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public SqliteRecipeRepository(IAppPaths paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                {
                    return;
                }

                _db ??= new SQLiteAsyncConnection(_paths.DatabasePath);
                await _db.CreateTableAsync<RecipeEntity>();
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task CloseAsync()
        {
            if (_db is not null)
            {
                await _db.CloseAsync();
                _db = null;
            }

            _initialized = false;
            SQLiteAsyncConnection.ResetPool();
        }

        public async Task ReopenAsync()
        {
            _db = new SQLiteAsyncConnection(_paths.DatabasePath);
            _initialized = false;
            await InitializeAsync();
        }

        public async Task<RecipePage> GetPageAsync(int page, int pageSize, string? searchText = null, bool favoriteOnly = false)
        {
            await InitializeAsync();

            var query = _db!.Table<RecipeEntity>();

            if (favoriteOnly)
            {
                query = query.Where(r => r.IsFavorite);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                // sqlite-net's LINQ provider translates Contains to a case-sensitive LIKE, so
                // lower-case both sides ourselves rather than relying on collation. Searching the
                // ingredients too means "what can I make with chicken" just works.
                var needle = searchText.Trim().ToLowerInvariant();
                query = query.Where(r =>
                    r.Title.ToLower().Contains(needle) ||
                    r.IngredientsText.ToLower().Contains(needle) ||
                    (r.Category != null && r.Category.ToLower().Contains(needle)));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.DateAdded)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new RecipePage
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<RecipeEntity?> GetByIdAsync(int id)
        {
            await InitializeAsync();
            return await _db!.Table<RecipeEntity>().Where(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task<RecipeEntity?> GetBySourceUrlAsync(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return null;
            }

            await InitializeAsync();
            return await _db!.Table<RecipeEntity>().Where(r => r.SourceUrl == sourceUrl).FirstOrDefaultAsync();
        }

        public async Task<int> SaveAsync(RecipeEntity recipe)
        {
            await InitializeAsync();

            recipe.DateModified = DateTime.UtcNow;

            if (recipe.Id == 0)
            {
                await _db!.InsertAsync(recipe);
            }
            else
            {
                await _db!.UpdateAsync(recipe);
            }

            return recipe.Id;
        }

        public async Task DeleteAsync(int id)
        {
            await InitializeAsync();
            await _db!.DeleteAsync<RecipeEntity>(id);
        }

        public async Task<int> GetCountAsync()
        {
            await InitializeAsync();
            return await _db!.Table<RecipeEntity>().CountAsync();
        }
    }
}
