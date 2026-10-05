namespace My_Recipe_Keeper.Core.Interfaces
{
    /// <summary>Wraps MAUI's FileSystem.AppDataDirectory so Core has no Maui dependency and stays unit-testable.</summary>
    public interface IAppPaths
    {
        string AppDataDirectory { get; }
        string DatabasePath { get; }
        string SettingsFilePath { get; }
    }

    /// <summary>Wraps MAUI's SecureStorage for OAuth token persistence.</summary>
    public interface ISecureTokenStore
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string value);
        void Remove(string key);
    }

    /// <summary>Opens a URL in the system browser (MAUI Launcher/Browser) for the OAuth consent screen.</summary>
    public interface IBrowserLauncher
    {
        Task OpenAsync(Uri uri);
    }

    /// <summary>
    /// Runs the OAuth "authorization code" leg: opens the consent page (via IBrowserLauncher) and
    /// captures the redirect. Implemented on the Maui head with a loopback HttpListener.
    /// </summary>
    public interface IAuthorizationCodeProvider
    {
        Task<string?> GetAuthorizationCodeAsync(Uri authUrl, Uri redirectUri, CancellationToken ct = default);
    }
}
