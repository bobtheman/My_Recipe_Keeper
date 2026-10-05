using System.Diagnostics;
using Plugin.Maui.OCR;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Services
{
    /// <summary>
    /// ML Kit text recognition (on-device, free) over screenshots and screen recordings. Video is sampled
    /// once a second — recipe overlays stay up for a few seconds, so that catches each one at least once
    /// while keeping a one-minute reel to ~60 OCR passes.
    /// </summary>
    public class MauiMediaTextService : IMediaTextService
    {
        private const long FrameIntervalMicroseconds = 1_000_000;
        private const int MaxFrames = 240; // 4 minutes of video
        private const int MaxFrameWidth = 1080;

        private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".3gp", ".mkv", ".mov" };

        public async Task<IReadOnlyList<string>> ReadTextBlocksAsync(IReadOnlyList<string> mediaPaths, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            await OcrPlugin.Default.InitAsync(ct);

            var blocks = new List<string>();
            for (var i = 0; i < mediaPaths.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var path = mediaPaths[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                if (IsVideo(path))
                {
                    blocks.AddRange(await ReadVideoAsync(path, progress, ct));
                }
                else
                {
                    progress?.Report(mediaPaths.Count == 1 ? "Reading screenshot…" : $"Reading screenshot {i + 1} of {mediaPaths.Count}…");
                    var bytes = await File.ReadAllBytesAsync(path, ct);
                    blocks.Add(await RecognizeAsync(bytes, ct));
                }
            }

            return OverlayTextParser.DistinctBlocks(blocks);
        }

        private static bool IsVideo(string path) =>
            VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private static async Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken ct)
        {
            try
            {
                var result = await OcrPlugin.Default.RecognizeTextAsync(imageBytes, tryHard: true, ct);
                return result.Success ? OverlayTextParser.CleanOcrText(result.Lines) : string.Empty;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"[MauiMediaTextService] OCR failed: {ex}");
                return string.Empty;
            }
        }

        private static async Task<List<string>> ReadVideoAsync(string path, IProgress<string>? progress, CancellationToken ct)
        {
            var blocks = new List<string>();
            using var retriever = new Android.Media.MediaMetadataRetriever();
            retriever.SetDataSource(path);

            var durationMs = long.TryParse(retriever.ExtractMetadata(Android.Media.MetadataKey.Duration), out var d) ? d : 0;
            var frameCount = (int)Math.Clamp(durationMs * 1000 / FrameIntervalMicroseconds, 1, MaxFrames);

            for (var frame = 0; frame < frameCount; frame++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Reading video… {frame + 1} of {frameCount} seconds");

                // Frame decode + JPEG encode is CPU work — keep it off the UI thread.
                var bytes = await Task.Run(() => GrabFrame(retriever, frame * FrameIntervalMicroseconds), ct);
                if (bytes is not null)
                {
                    blocks.Add(await RecognizeAsync(bytes, ct));
                }
            }

            return blocks;
        }

        private static byte[]? GrabFrame(Android.Media.MediaMetadataRetriever retriever, long timeUs)
        {
            using var bitmap = retriever.GetFrameAtTime(timeUs, Android.Media.Option.ClosestSync);
            if (bitmap is null)
            {
                return null;
            }

            var scaled = bitmap;
            if (bitmap.Width > MaxFrameWidth)
            {
                var height = (int)(bitmap.Height * (MaxFrameWidth / (double)bitmap.Width));
                scaled = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, MaxFrameWidth, height, true);
            }

            try
            {
                using var stream = new MemoryStream();
                // CompressFormat.Jpeg is a static enum value and never null — the binding marks it nullable.
                scaled!.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, stream);
                return stream.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(scaled, bitmap))
                {
                    scaled?.Dispose();
                }
            }
        }
    }
}
