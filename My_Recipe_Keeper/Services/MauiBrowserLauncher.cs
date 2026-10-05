using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    public class MauiBrowserLauncher : IBrowserLauncher
    {
        public Task OpenAsync(Uri uri) => Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }
}
