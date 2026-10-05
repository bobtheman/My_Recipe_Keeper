using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Tests.TestSupport
{
    /// <summary>Points every path at a fresh temp directory so tests never touch a real device profile.</summary>
    public sealed class FakeAppPaths : IAppPaths, IDisposable
    {
        public string AppDataDirectory { get; }
        public string DatabasePath { get; }
        public string SettingsFilePath { get; }

        public FakeAppPaths()
        {
            AppDataDirectory = Path.Combine(Path.GetTempPath(), "recipekeeper-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(AppDataDirectory);
            DatabasePath = Path.Combine(AppDataDirectory, "test.db3");
            SettingsFilePath = Path.Combine(AppDataDirectory, "settings.json");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(AppDataDirectory, recursive: true);
            }
            catch
            {
                // best-effort cleanup; leaked temp dirs don't fail the test run
            }
        }
    }
}
