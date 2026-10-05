using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Components.Pages
{
    public partial class Home : IDisposable
    {
        private const int PageSize = 20;
        private int _currentPage = 1;
        private int _totalCount;
        private bool _favoriteOnly;
        private bool _loading = true;
        private string _searchText = string.Empty;
        private RecipePage _page = new();
        private readonly HashSet<int> _brokenImages = new();
        private CancellationTokenSource? _searchDebounce;

        protected override async Task OnInitializedAsync()
        {
            _totalCount = await RecipeRepository.GetCountAsync();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _loading = _page.Items.Count == 0;
            StateHasChanged();

            _page = await RecipeRepository.GetPageAsync(_currentPage, PageSize, _searchText, _favoriteOnly);

            _loading = false;
            StateHasChanged();
        }

        // Search-as-you-type, debounced so each keystroke doesn't hit SQLite.
        private async Task OnSearchChangedAsync()
        {
            _searchDebounce?.Cancel();
            _searchDebounce = new CancellationTokenSource();
            var token = _searchDebounce.Token;

            try
            {
                await Task.Delay(250, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            _currentPage = 1;
            await LoadAsync();
        }

        private Task ClearSearchAsync()
        {
            _searchText = string.Empty;
            _currentPage = 1;
            return LoadAsync();
        }

        private Task SetFavoriteOnlyAsync(bool value)
        {
            _favoriteOnly = value;
            _currentPage = 1;
            return LoadAsync();
        }

        private Task GoToPageAsync(int page)
        {
            _currentPage = page;
            return LoadAsync();
        }

        private void OpenRecipe(int id) => Nav.NavigateTo($"/recipe/{id}");

        private void AddRecipe() => Nav.NavigateTo("/recipe/add");

        private static string CardMeta(RecipeEntity recipe)
        {
            var parts = new List<string>();
            var time = recipe.TotalTime ?? recipe.CookTime;
            if (!string.IsNullOrWhiteSpace(time))
            {
                parts.Add($"⏱ {time}");
            }

            var count = recipe.Ingredients.Count(i => !i.EndsWith(':'));
            if (count > 0)
            {
                parts.Add($"🥕 {count}");
            }

            return parts.Count > 0 ? string.Join("  ", parts) : "✨ Ready to cook";
        }

        public void Dispose()
        {
            _searchDebounce?.Cancel();
            _searchDebounce?.Dispose();
        }
    }
}
