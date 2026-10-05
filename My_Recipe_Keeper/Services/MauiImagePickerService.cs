using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    public class MauiImagePickerService : IImagePickerService
    {
        private static readonly FilePickerFileType ScreenshotsAndRecordings = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "image/*", "video/*" } }
        });

        public async Task<IReadOnlyList<string>> PickMediaAsync()
        {
            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Pick screenshots or a screen recording",
                FileTypes = ScreenshotsAndRecordings
            });

            // FilePicker on Android already copies each pick into the app cache, so FullPath is readable.
            return results?.Where(r => r is not null).Select(r => r!.FullPath).ToList() ?? new List<string>();
        }
    }
}
