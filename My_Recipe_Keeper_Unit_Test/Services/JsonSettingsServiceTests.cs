using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class JsonSettingsServiceTests : IDisposable
    {
        private readonly FakeAppPaths _paths = new();
        private readonly JsonSettingsService _sut;

        public JsonSettingsServiceTests()
        {
            _sut = new JsonSettingsService(_paths);
        }

        public void Dispose() => _paths.Dispose();

        [Fact]
        public async Task LoadAsync_NoFileYet_ReturnsDefaults()
        {
            var settings = await _sut.LoadAsync();

            Assert.False(settings.AutoBackupEnabled);
            Assert.Equal("System", settings.ThemeMode);
            Assert.True(settings.IncludeSourceLinkWhenSharing);
        }

        [Fact]
        public async Task SaveThenLoad_RoundTripsValues()
        {
            await _sut.SaveAsync(new AppSettingsModel { AutoBackupEnabled = true, ThemeMode = "Dark", IncludeSourceLinkWhenSharing = false });

            var settings = await _sut.LoadAsync();

            Assert.True(settings.AutoBackupEnabled);
            Assert.Equal("Dark", settings.ThemeMode);
            Assert.False(settings.IncludeSourceLinkWhenSharing);
        }
    }
}
