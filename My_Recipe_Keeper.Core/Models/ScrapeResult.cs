namespace My_Recipe_Keeper.Core.Models
{
    public enum ScrapeSource
    {
        None,
        StructuredData,
        SocialCaption,
        PageMetadata
    }

    public class ScrapeResult
    {
        public bool Success => Recipe is not null;

        public RecipeEntity? Recipe { get; set; }

        public ScrapeSource Source { get; set; }

        public string? Error { get; set; }

        /// <summary>Source language code (e.g. "tr") when the recipe was translated; null otherwise.</summary>
        public string? TranslatedFrom { get; set; }

        /// <summary>The raw caption/meta text the recipe was parsed from, so it can be translated and re-parsed.</summary>
        internal CaptionInput? Caption { get; set; }

        public static ScrapeResult Fail(string error) => new() { Error = error };
    }

    internal sealed record CaptionInput(string? Caption, string? PageTitle, string? Image, string SourceUrl, bool PreferCaptionTitle);
}
