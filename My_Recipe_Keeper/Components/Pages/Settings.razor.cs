using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Components.Pages
{
    public partial class Settings
    {
        private bool _signedIn;
        private bool _busy;
        private string? _status;
        private bool _statusIsError;
        private AppSettingsModel _settings = new();
        private int _recipeCount;

        protected override async Task OnInitializedAsync()
        {
            _signedIn = await BackupService.IsSignedInAsync();
            _settings = await SettingsService.LoadAsync();
            _recipeCount = await RecipeRepository.GetCountAsync();
        }

        private async Task SignInAsync() => await RunAsync(async () =>
        {
            _signedIn = await BackupService.SignInAsync();
            SetStatus(_signedIn ? "Signed in." : "Sign-in was cancelled.", isError: !_signedIn);
        });

        private async Task SignOutAsync() => await RunAsync(async () =>
        {
            await BackupService.SignOutAsync();
            _signedIn = false;
            SetStatus("Signed out.");
        });

        private async Task BackupAsync() => await RunAsync(async () =>
        {
            var ok = await BackupService.BackupAsync();
            SetStatus(ok ? "Backup complete. 🎉" : "Backup failed.", isError: !ok);
        });

        private async Task RestoreAsync() => await RunAsync(async () =>
        {
            var ok = await BackupService.RestoreAsync();
            SetStatus(ok ? "Restore complete." : "No backup found.", isError: !ok);
            _recipeCount = await RecipeRepository.GetCountAsync();
        });

        private async Task OnAutoBackupChanged(ChangeEventArgs args)
        {
            _settings.AutoBackupEnabled = args.Value is bool value && value;
            await SaveSettingsAsync();
        }

        private async Task OnIncludeLinkChanged(ChangeEventArgs args)
        {
            _settings.IncludeSourceLinkWhenSharing = args.Value is bool value && value;
            await SaveSettingsAsync();
        }

        private async Task OnAutoTranslateChanged(ChangeEventArgs args)
        {
            _settings.AutoTranslate = args.Value is bool value && value;
            await SaveSettingsAsync();
        }

        private Task SaveSettingsAsync() => SettingsService.SaveAsync(_settings);

        private async Task OnThemeChangedAsync()
        {
            await SaveSettingsAsync();
            await JsRuntime.InvokeVoidAsync("recipeKeeper.applyTheme", _settings.ThemeMode);
        }

        private async Task RunAsync(Func<Task> action)
        {
            _busy = true;
            _status = null;
            StateHasChanged();

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", isError: true);
            }
            finally
            {
                _busy = false;
            }
        }

        private void SetStatus(string message, bool isError = false)
        {
            _status = message;
            _statusIsError = isError;
        }
    }
}
