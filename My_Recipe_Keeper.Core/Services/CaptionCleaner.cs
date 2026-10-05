using System.Text.RegularExpressions;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Strips social-media noise from captions: hashtags, @mention-only lines and call-to-action lines
    /// ("Comment RECIPE and I'll send you the link", "Follow me @x", "Link in bio", "Save this for later").
    /// </summary>
    public static class CaptionCleaner
    {
        private static readonly Regex Hashtag = new(@"(?<![\p{L}\p{N}&])#[\p{L}\p{N}_]+", RegexOptions.Compiled);

        private static readonly Regex Mention = new(@"(?<![\p{L}\p{N}])@[\p{L}\p{N}_.]+", RegexOptions.Compiled);

        private static readonly Regex CallToAction = new(
            @"send (you|u|it|them)\b.{0,20}\b(link|recipe)" +
            @"|\b(comment|dm|message|type)\b.{0,40}\b(link|recipe|send|below)\b" +
            @"|\blink (is )?in (my |the )?(bio|profile|description|comments?|stories|story)" +
            @"|\b(recipe|full recipe|details?) (link )?(is )?(on|at|in) (my )?(website|blog|bio|profile|site|substack|youtube|channel)" +
            @"|\bsearch\b.{0,40}\bon (my )?(website|blog|site)" +
            @"|\bfollow (me|us|along|for|@)" +
            @"|\bfor more (recipes|ideas|content)" +
            @"|\bcheck out my\b" +
            @"|\bsave (this|it|for later|the recipe)\b" +
            @"|\btag (a|someone|your|a friend)\b" +
            @"|\bshare (this|with)\b" +
            @"|\bturn on (post )?notifications\b" +
            @"|\bsubscribe\b" +
            @"|\b(like|likes?) (and|&) (follow|save|share)\b" +
            @"|\bpre-?order my (cook)?book\b" +
            @"|\bmy (new )?(cook)?book is (out|available)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LeftoverPunctuation = new(@"^[^\p{L}\p{N}]*$", RegexOptions.Compiled);

        private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

        /// <summary>Cleaned line, or null if the whole line is noise and should be dropped.</summary>
        public static string? CleanLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            if (CallToAction.IsMatch(line))
            {
                return null;
            }

            var cleaned = Hashtag.Replace(line, string.Empty);

            // A line of only mentions ("@chef @brand") is credit noise; a mention inside a sentence
            // ("Made with @brand mixer") keeps the sentence but loses the handle's @.
            var withoutMentions = Mention.Replace(cleaned, string.Empty);
            if (LeftoverPunctuation.IsMatch(withoutMentions))
            {
                return null;
            }

            cleaned = Mention.Replace(cleaned, m => m.Value[1..]);
            cleaned = Spaces.Replace(cleaned, " ").Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>Cleans a multi-line block, keeping paragraph breaks.</summary>
        public static string? CleanBlock(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>();
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    // Collapse runs of blank lines left behind by removed noise.
                    if (kept.Count > 0 && kept[^1].Length > 0)
                    {
                        kept.Add(string.Empty);
                    }

                    continue;
                }

                var cleaned = CleanLine(line);
                if (cleaned is not null)
                {
                    kept.Add(cleaned);
                }
            }

            var result = string.Join("\n", kept).Trim();
            return result.Length == 0 ? null : result;
        }
    }
}
