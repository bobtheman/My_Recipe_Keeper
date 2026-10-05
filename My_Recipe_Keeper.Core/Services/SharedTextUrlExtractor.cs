using System.Text.RegularExpressions;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Apps share links wrapped in prose ("Check out this reel! https://..."), so the first http(s)
    /// URL is pulled out of the text rather than expecting the whole payload to be a URL.
    /// </summary>
    public static class SharedTextUrlExtractor
    {
        private static readonly Regex UrlPattern = new(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BareDomainPattern = new(@"^(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)+(/\S*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] TrailingPunctuation = { '.', ',', '!', '?', ')', ']', '}', ';', ':' };

        public static string? Extract(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = UrlPattern.Match(text);
            if (match.Success)
            {
                var url = match.Value.TrimEnd(TrailingPunctuation);
                return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : null;
            }

            // Typed without a scheme, e.g. "bbcgoodfood.com/recipes/easy-pancakes".
            var trimmed = text.Trim();
            if (BareDomainPattern.IsMatch(trimmed))
            {
                var url = "https://" + trimmed.TrimEnd(TrailingPunctuation);
                return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : null;
            }

            return null;
        }
    }
}
