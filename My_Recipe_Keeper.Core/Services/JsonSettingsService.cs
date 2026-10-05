using System.Text.Json;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    public class JsonSettingsService : ISettingsService
    {
        private readonly IAppPaths _paths;

        public JsonSettingsService(IAppPaths paths)
        {
            _paths = paths;
        }

        public async Task<AppSettingsModel> LoadAsync()
        {
            if (!File.Exists(_paths.SettingsFilePath))
            {
                return new AppSettingsModel();
            }

            await using var stream = File.OpenRead(_paths.SettingsFilePath);
            return await JsonSerializer.DeserializeAsync<AppSettingsModel>(stream) ?? new AppSettingsModel();
        }

        public async Task SaveAsync(AppSettingsModel settings)
        {
            await using var stream = File.Create(_paths.SettingsFilePath);
            await JsonSerializer.SerializeAsync(stream, settings);
        }
    }
}
