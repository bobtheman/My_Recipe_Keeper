using System.Text.RegularExpressions;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Parses text OCR'd from video screenshots (recipe reels that put the recipe on screen rather than
    /// in the caption). Each screenshot is one block, separated by a blank line: a block becomes a
    /// method step, and every "quantity + unit + thing" inside it becomes an ingredient — e.g.
    /// "Cream together 230g light brown sugar, 110g caster sugar &amp; 200g butter" yields one step and
    /// three ingredients. If the text already has "Ingredients:"/"Method:" headings (a recipe card
    /// screenshot) the caption parser handles it instead.
    /// </summary>
    public static class OverlayTextParser
    {
        private const string Units =
            @"kg|g|grams?|mg|ml|millilitres?|milliliters?|l|litres?|liters?|cl|oz|ounces?|lbs?|pounds?|" +
            @"tbsps?|tablespoons?|tbs|tsps?|teaspoons?|cups?|pints?|cloves?|tins?|cans?|packs?|packets?|" +
            @"sticks?|slices?|pinch(?:es)?|handfuls?|bunch(?:es)?|sprigs?";

        private const string Number = @"(?:\d+(?:[.,/]\d+)?(?:\s*-\s*\d+(?:[.,/]\d+)?)?|[½⅓⅔¼¾⅕⅛]|\d+\s*[½⅓⅔¼¾⅛])";

        // Quantity-led ingredient: "230g light brown sugar", "2 tbsp honey", "3 eggs", "½ cup milk".
        private static readonly Regex QuantityIngredient = new(
            $@"(?<![\w.])(?<qty>{Number})\s*(?<unit>(?:{Units})\b\.?)?\s*(?:of\s+)?(?<item>[\p{{L}}][\p{{L}}'’\- ]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Where an ingredient phrase stops and the instruction carries on.
        private static readonly Regex IngredientTail = new(
            @"\s+(for|till|until|then|into|in|on|onto|with|to|at|and then|before|after|while|whilst|over|together|so|if)\b.*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ItemSeparator = new(@"\s*(?:,|&|\+|\band\b|;)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Timers/temperatures/durations look like quantities but aren't ingredients.
        private static readonly Regex NotIngredient = new(
            @"^(mins?|minutes?|hours?|hrs?|secs?|seconds?|degrees?|c|f|°|fan|times?|x|days?|weeks?|servings?|people|portions?|cookies|pieces|layers?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NoiseLine = new(@"^[\d\s:.,%/\-|•·]*$", RegexOptions.Compiled);

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Light clean-up of raw OCR lines from one screenshot before the user reviews them: drops
        /// clock readouts ("02:51"), stray symbols and 1–2 character fragments.
        /// </summary>
        public static string CleanOcrText(IEnumerable<string> lines)
        {
            var kept = lines
                .Select(l => l.Trim())
                .Where(l => l.Length > 2 && !NoiseLine.IsMatch(l));
            return string.Join("\n", kept);
        }

        /// <summary>
        /// Collapses OCR blocks from consecutive video frames: the same overlay stays on screen for several
        /// seconds and OCR jitters slightly frame to frame, or catches it half faded in. Similar neighbours
        /// (≥60% shared words) are merged, keeping the most complete reading. Exact repeats anywhere are dropped.
        /// </summary>
        public static IReadOnlyList<string> DistinctBlocks(IEnumerable<string> blocks)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in blocks)
            {
                var block = raw?.Trim() ?? string.Empty;
                var key = Whitespace.Replace(block, " ");
                if (key.Length == 0 || seen.Contains(key))
                {
                    continue;
                }

                if (result.Count > 0 && Similarity(result[^1], block) >= 0.6)
                {
                    if (block.Length > result[^1].Length)
                    {
                        result[^1] = block;
                    }

                    seen.Add(key);
                    continue;
                }

                seen.Add(key);
                result.Add(block);
            }

            return result;
        }

        private static double Similarity(string a, string b)
        {
            static HashSet<string> Words(string s) =>
                new(Regex.Matches(s.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value));

            var wordsA = Words(a);
            var wordsB = Words(b);
            if (wordsA.Count == 0 || wordsB.Count == 0)
            {
                return 0;
            }

            // Overlap relative to the smaller set, so a half-faded-in frame still matches the full one.
            var shared = wordsA.Count(wordsB.Contains);
            return (double)shared / Math.Min(wordsA.Count, wordsB.Count);
        }

        public static ParsedRecipeText Parse(string? reviewedText)
        {
            if (string.IsNullOrWhiteSpace(reviewedText))
            {
                return new ParsedRecipeText();
            }

            var normalised = reviewedText.Replace("\r\n", "\n").Replace('\r', '\n');

            var structured = RecipeTextParser.Parse(normalised);
            if (structured.UsedHeadings && structured.Instructions.Count + structured.Ingredients.Count > 0)
            {
                return structured;
            }

            var result = new ParsedRecipeText();
            var seenSteps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIngredients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var block in Regex.Split(normalised, @"\n\s*\n"))
            {
                // Overlay text wraps mid-sentence ("Cream together\n230g light brown sugar,\n...") — one block, one step.
                var lines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                if (lines.Count == 0)
                {
                    continue;
                }

                var step = Whitespace.Replace(string.Join(" ", lines), " ").Trim();

                // Consecutive screenshots of the same caption card are common — keep one copy.
                if (seenSteps.Add(step))
                {
                    result.Instructions.Add(step);
                }

                foreach (var line in lines)
                {
                    foreach (var ingredient in ExtractIngredients(line))
                    {
                        if (seenIngredients.Add(ingredient))
                        {
                            result.Ingredients.Add(ingredient);
                        }
                    }
                }
            }

            return result;
        }

        public static IReadOnlyList<string> ExtractIngredients(string text)
        {
            var found = new List<string>();
            foreach (var chunk in ItemSeparator.Split(text))
            {
                var match = QuantityIngredient.Match(chunk);
                if (!match.Success)
                {
                    continue;
                }

                var item = IngredientTail.Replace(match.Groups["item"].Value, string.Empty).Trim(' ', '-', '.');
                if (item.Length < 2 || NotIngredient.IsMatch(item))
                {
                    continue;
                }

                var unit = match.Groups["unit"].Value.Trim();
                var qty = match.Groups["qty"].Value.Trim();
                // "230g sugar" stays tight, "2 tbsp honey" gets a space — mirrors how recipes are written.
                var quantity = unit.Length == 0 ? qty : unit.Length <= 2 ? qty + unit : $"{qty} {unit}";
                found.Add($"{quantity} {item}");
            }

            return found;
        }
    }
}
