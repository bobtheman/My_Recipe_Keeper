using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    /// <summary>Native Android confirm dialog via MAUI's DisplayAlert — not a JS confirm(), so it
    /// looks and behaves like the rest of the OS, independent of the WebView's dialog support.</summary>
    public class MauiAlertService : IAlertService
    {
        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", string cancelText = "Cancel")
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            return page is null
                ? Task.FromResult(false)
                : page.DisplayAlertAsync(title, message, confirmText, cancelText);
        }
    }
}
