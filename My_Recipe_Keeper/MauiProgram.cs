using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Services;

namespace My_Recipe_Keeper
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Must run before any SQLiteAsyncConnection is touched (see Pok_E_List: concurrent first
            // use crashed with "pthread_mutex_lock called on a destroyed mutex" without it).
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // Embedded appsettings.json — real Google client id/secret are supplied per-build, never committed.
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("My_Recipe_Keeper.appsettings.json");
            var config = new ConfigurationBuilder().AddJsonStream(stream!).Build();
            builder.Configuration.AddConfiguration(config);

            builder.Services.AddSingleton(config.GetSection("Scraper").Get<ScraperOptions>() ?? new ScraperOptions());
            builder.Services.AddSingleton(config.GetSection("Google").Get<GoogleAuthOptions>() ?? new GoogleAuthOptions());
            builder.Services.AddSingleton(config.GetSection("Translation").Get<TranslationOptions>() ?? new TranslationOptions());

            // Platform adapters (Maui-specific), injected into the Maui-free Core services below.
            builder.Services.AddSingleton<IAppPaths, MauiAppPaths>();
            builder.Services.AddSingleton<ISecureTokenStore, SecureStorageTokenStore>();
            builder.Services.AddSingleton<IBrowserLauncher, MauiBrowserLauncher>();
            builder.Services.AddSingleton<IAuthorizationCodeProvider, LoopbackAuthorizationCodeProvider>();
            builder.Services.AddSingleton<IAlertService, MauiAlertService>();
            builder.Services.AddSingleton<IShareService, MauiShareService>();
            builder.Services.AddSingleton<IMediaTextService, MauiMediaTextService>();
            builder.Services.AddSingleton<IImagePickerService, MauiImagePickerService>();

            // Core services — plain .NET, unit tested independently of Maui in My_Recipe_Keeper_Unit_Test.
            // Singleton repository: backup/restore needs every consumer on the same connection.
            builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
            builder.Services.AddSingleton<ISettingsService, JsonSettingsService>();
            builder.Services.AddSingleton<IRecipeShareFormatter, RecipeShareFormatter>();
            builder.Services.AddSingleton<ISharedLinkInbox>(SharedLinks.Inbox);
            builder.Services.AddHttpClient<ITranslationService, FreeTranslationService>();
            builder.Services.AddTransient<IRecipeTranslator, RecipeTranslator>();
            builder.Services.AddHttpClient<IRecipeScraperService, RecipeScraperService>();
            builder.Services.AddHttpClient<IGoogleBackupService, GoogleDriveBackupService>();
            builder.Services.AddSingleton<AutoBackupRunner>();

            var app = builder.Build();
            return app;
        }
    }
}
