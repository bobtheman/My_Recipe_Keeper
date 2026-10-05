using System.Diagnostics;
using Android.Content;

namespace My_Recipe_Keeper.Platforms.Android
{
    /// <summary>
    /// Copies media shared into the app (content:// URIs, only readable while the share grant lasts)
    /// into the cache directory so OCR can read them later from a plain file path.
    /// </summary>
    public static class SharedMediaImporter
    {
        public static List<string> CopyToCache(Context context, IEnumerable<global::Android.Net.Uri> uris)
        {
            var directory = Path.Combine(context.CacheDir!.AbsolutePath, "shared-media");
            Directory.CreateDirectory(directory);

            var paths = new List<string>();
            foreach (var uri in uris)
            {
                try
                {
                    var mimeType = context.ContentResolver?.GetType(uri) ?? string.Empty;
                    var extension = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? ".mp4" : ".jpg";
                    var destination = Path.Combine(directory, $"{Guid.NewGuid()}{extension}");

                    using var input = context.ContentResolver!.OpenInputStream(uri);
                    if (input is null)
                    {
                        continue;
                    }

                    using var output = File.Create(destination);
                    input.CopyTo(output);
                    paths.Add(destination);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SharedMediaImporter] {uri}: {ex}");
                }
            }

            return paths;
        }
    }
}
