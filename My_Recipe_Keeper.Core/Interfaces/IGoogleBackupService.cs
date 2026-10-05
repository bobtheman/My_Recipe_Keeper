namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IGoogleBackupService
    {
        Task<bool> IsSignedInAsync();

        Task<bool> SignInAsync(CancellationToken ct = default);

        Task SignOutAsync();

        /// <summary>Closes the local db, uploads it, deletes older backups, reopens the db.</summary>
        Task<bool> BackupAsync(CancellationToken ct = default);

        /// <summary>Downloads the newest backup, closes the local db, replaces the file, reopens the db.</summary>
        Task<bool> RestoreAsync(CancellationToken ct = default);
    }

    public interface ISettingsService
    {
        Task<Models.AppSettingsModel> LoadAsync();
        Task SaveAsync(Models.AppSettingsModel settings);
    }
}
