using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IRecipeScraperService
    {
        /// <summary>
        /// Accepts a bare URL or any shared text containing one (e.g. "Look at this! https://...").
        /// Never throws for network/parse failures — they come back as a failed ScrapeResult.
        /// translateTo: language code to translate a foreign recipe into (null = keep original language).
        /// </summary>
        Task<ScrapeResult> ScrapeAsync(string urlOrSharedText, string? translateTo = null, CancellationToken ct = default);
    }
}
