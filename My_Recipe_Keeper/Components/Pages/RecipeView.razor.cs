using Microsoft.AspNetCore.Components;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Components.Pages
{
    public partial class RecipeView
    {
        [Parameter]
        public int Id { get; set; }

        private RecipeEntity? _recipe;
        private bool _loaded;
        private bool _imageBroken;
        private bool _includeSourceLink = true;
        private ShareFormat _format = ShareFormat.ShoppingList;
        private readonly HashSet<string> _have = new(StringComparer.OrdinalIgnoreCase);
        private string? _toast;
        private CancellationTokenSource? _toastCts;

        private bool HasFacts =>
            _recipe is not null &&
            new[] { _recipe.Servings, _recipe.PrepTime, _recipe.CookTime, _recipe.TotalTime, _recipe.Category }.Any(v => !string.IsNullOrWhiteSpace(v));

        private string ShareText => _recipe is null
            ? string.Empty
            : ShareFormatter.Format(_recipe, _format, _have, _includeSourceLink);

        protected override async Task OnParametersSetAsync()
        {
            _recipe = await RecipeRepository.GetByIdAsync(Id);
            _includeSourceLink = (await SettingsService.LoadAsync()).IncludeSourceLinkWhenSharing;
            _have.Clear();
            _imageBroken = false;
            _loaded = true;
        }

        private static bool IsGroupLabel(string line) => RecipeShareFormatter.IsGroupLabel(line);

        private void ToggleHave(string line)
        {
            if (!_have.Remove(line))
            {
                _have.Add(line);
            }
        }

        private async Task ToggleFavoriteAsync()
        {
            if (_recipe is null)
            {
                return;
            }

            _recipe.IsFavorite = !_recipe.IsFavorite;
            await RecipeRepository.SaveAsync(_recipe);
            AutoBackup.TriggerIfEnabled();
            ShowToast(_recipe.IsFavorite ? "❤️ Added to favourites" : "Removed from favourites");
        }

        private async Task ShareWhatsAppAsync()
        {
            var sent = await ShareService.ShareToWhatsAppAsync(ShareText);
            if (!sent)
            {
                // No WhatsApp installed — the share sheet still gets it somewhere useful.
                ShowToast("WhatsApp not found — pick another app");
                await ShareService.ShareTextAsync(_recipe!.Title, ShareText);
            }
        }

        private Task ShareSheetAsync() => ShareService.ShareTextAsync(_recipe!.Title, ShareText);

        private async Task CopyAsync()
        {
            await ShareService.CopyToClipboardAsync(ShareText);
            ShowToast("📋 Copied — paste it anywhere");
        }

        private async Task OpenSourceAsync()
        {
            // Source link is user-editable, so it may not be a valid absolute URL.
            if (Uri.TryCreate(_recipe?.SourceUrl, UriKind.Absolute, out var uri))
            {
                await BrowserLauncher.OpenAsync(uri);
            }
            else
            {
                ShowToast("That link doesn't look right");
            }
        }

        private void Edit() => Nav.NavigateTo($"/recipe/edit/{Id}");

        private void GoHome() => Nav.NavigateTo("/");

        private void ShowToast(string message)
        {
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;
            _toast = message;

            _ = Task.Delay(2200, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    _ = InvokeAsync(() =>
                    {
                        _toast = null;
                        StateHasChanged();
                    });
                }
            }, TaskScheduler.Default);
        }

        public void Dispose()
        {
            _toastCts?.Cancel();
            _toastCts?.Dispose();
        }
    }
}
