namespace My_Recipe_Keeper.Core.Interfaces
{
    public class TranslationResult
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>Detected source language code, e.g. "tr". Equal to the target when nothing needed translating.</summary>
        public string SourceLanguage { get; set; } = string.Empty;
    }

    public interface ITranslationService
    {
        /// <summary>Auto-detects the source language. Returns null when the service can't be reached.</summary>
        Task<TranslationResult?> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default);
    }
}
