using System.Diagnostics;
using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    /// <summary>Fire-and-forget Drive backup after a save/delete when the user has turned it on.</summary>
    public class AutoBackupRunner
    {
        private readonly ISettingsService _settingsService;
        private readonly IGoogleBackupService _backupService;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public AutoBackupRunner(ISettingsService settingsService, IGoogleBackupService backupService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        }

        public void TriggerIfEnabled() => _ = RunAsync();

        private async Task RunAsync()
        {
            // Skip rather than queue: back-to-back edits shouldn't stack uploads. A change skipped here
            // goes up with the next save's backup (or a manual "Back up now").
            if (!await _gate.WaitAsync(0))
            {
                return;
            }

            try
            {
                var settings = await _settingsService.LoadAsync();
                if (settings.AutoBackupEnabled && await _backupService.IsSignedInAsync())
                {
                    await _backupService.BackupAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoBackupRunner] {ex}");
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
