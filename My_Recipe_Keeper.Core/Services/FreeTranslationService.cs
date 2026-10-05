using System.Text;
using System.Text.Json;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Free, key-less translation via the endpoint Google's own Chrome dictionary extension uses. Auto-detects
    /// the source language and keeps line breaks, which the recipe parser relies on for structure.
    /// It's unofficial: if Google changes or rate-limits it, calls return null and the recipe is kept
    /// in its original language rather than failing the import.
    /// </summary>
    public class FreeTranslationService : ITranslationService
    {
        // Comfortably under the form-post size the endpoint accepts; long captions go in paragraph chunks.
        private const int MaxChunkLength = 4000;

        private readonly HttpClient _httpClient;
        private readonly TranslationOptions _options;

        public FreeTranslationService(HttpClient httpClient, TranslationOptions options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<TranslationResult?> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TranslationResult { Text = text ?? string.Empty, SourceLanguage = targetLanguage };
            }

            var output = new StringBuilder();
            string? source = null;

            foreach (var chunk in Chunk(text))
            {
                var translated = await TranslateChunkAsync(chunk, targetLanguage, ct);
                if (translated is null)
                {
                    return null;
                }

                source ??= translated.Value.Source;

                // First chunk already in the target language → the rest is too; hand back the original untouched.
                if (string.Equals(source, targetLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return new TranslationResult { Text = text, SourceLanguage = source };
                }

                output.Append(translated.Value.Text);
                // Translators trim trailing whitespace; restore the line break between chunks.
                if (chunk.EndsWith('\n') && !translated.Value.Text.EndsWith('\n'))
                {
                    output.Append('\n');
                }
            }

            return new TranslationResult { Text = output.ToString(), SourceLanguage = source ?? string.Empty };
        }

        private async Task<(string Text, string Source)?> TranslateChunkAsync(string chunk, string targetLanguage, CancellationToken ct)
        {
            var url = $"{_options.Endpoint}?client=dict-chrome-ex&sl=auto&tl={Uri.EscapeDataString(targetLanguage)}";
            try
            {
                using var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("q", chunk) });
                using var response = await _httpClient.PostAsync(url, content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                return ParseResponse(body);
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        /// <summary>
        /// Shapes seen: [["translated","tr"]] (auto-detect) or ["translated"]. Public for tests.
        /// </summary>
        public static (string Text, string Source)? ParseResponse(string body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    return null;
                }

                var first = root[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    return (first.GetString() ?? string.Empty, string.Empty);
                }

                if (first.ValueKind == JsonValueKind.Array && first.GetArrayLength() > 0 && first[0].ValueKind == JsonValueKind.String)
                {
                    var source = first.GetArrayLength() > 1 && first[1].ValueKind == JsonValueKind.String ? first[1].GetString() : null;
                    return (first[0].GetString() ?? string.Empty, source ?? string.Empty);
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static IEnumerable<string> Chunk(string text)
        {
            if (text.Length <= MaxChunkLength)
            {
                yield return text;
                yield break;
            }

            // Split on line boundaries and keep the newline with each piece so the joined output
            // has the same line structure as the input.
            var current = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                if (current.Length > 0 && current.Length + line.Length + 1 > MaxChunkLength)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                current.Append(line).Append('\n');
            }

            if (current.Length > 0)
            {
                // Drop the newline added after the input's final line.
                yield return current.ToString(0, current.Length - 1);
            }
        }
    }
}
