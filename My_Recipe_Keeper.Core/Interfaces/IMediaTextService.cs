namespace My_Recipe_Keeper.Core.Interfaces
{
    /// <summary>
    /// On-device OCR (Google ML Kit via Plugin.Maui.OCR) — free, offline, no API key. Screenshots give one
    /// text block each; screen recordings are sampled about once a second, one block per frame.
    /// </summary>
    public interface IMediaTextService
    {
        /// <param name="progress">Reports a short status line ("Reading frame 12 of 48…").</param>
        Task<IReadOnlyList<string>> ReadTextBlocksAsync(IReadOnlyList<string> mediaPaths, IProgress<string>? progress = null, CancellationToken ct = default);
    }
}
