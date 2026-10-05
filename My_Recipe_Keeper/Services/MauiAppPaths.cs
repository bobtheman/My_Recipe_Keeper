using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    public class MauiAppPaths : IAppPaths
    {
        public string AppDataDirectory => FileSystem.AppDataDirectory;

        public string DatabasePath => Path.Combine(AppDataDirectory, "MyRecipeKeeper.db3");

        public string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");
    }
}
