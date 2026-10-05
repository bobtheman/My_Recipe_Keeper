using System.Net;
using System.Net.Http.Headers;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class FreeTranslationServiceTests
    {
        private static (FreeTranslationService Sut, RoutedHttpMessageHandler Handler) Build()
        {
            var handler = new RoutedHttpMessageHandler();
            return (new FreeTranslationService(new HttpClient(handler), new TranslationOptions()), handler);
        }

        [Fact]
        public async Task TranslateAsync_ForeignText_ReturnsTranslationAndDetectedLanguage()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Post, "translate_a/t", Json("""[["For the dough:\n1 cup warm milk","tr"]]"""));

            var result = await sut.TranslateAsync("Hamuru için:\n1 su bardağı ılık süt", "en");

            Assert.NotNull(result);
            Assert.Equal("tr", result!.SourceLanguage);
            Assert.Equal("For the dough:\n1 cup warm milk", result.Text);
            Assert.Contains("tl=en", handler.Requests.Single().RequestUri!.Query);
        }

        [Fact]
        public async Task TranslateAsync_AlreadyTarget_ReturnsOriginalText()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Post, "translate_a/t", Json("""[["2 eggs, beaten","en"]]"""));

            var result = await sut.TranslateAsync("2 eggs  beaten", "en");

            Assert.Equal("2 eggs  beaten", result!.Text);
            Assert.Equal("en", result.SourceLanguage);
        }

        [Fact]
        public async Task TranslateAsync_ServiceBlocked_ReturnsNull()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Post, "translate_a/t", new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            Assert.Null(await sut.TranslateAsync("Merhaba", "en"));
        }

        [Fact]
        public async Task TranslateAsync_LongText_ChunkedAndLineBreaksKept()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Post, "translate_a/t", _ => JsonResponse("""[["line","tr"]]"""));
            var text = string.Join("\n", Enumerable.Repeat(new string('a', 900), 6)); // ~5.4k chars

            var result = await sut.TranslateAsync(text, "en");

            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal("line\nline", result!.Text);
        }

        [Theory]
        [InlineData("""[["Hello","tr"]]""", "Hello", "tr")]
        [InlineData("""["Hello"]""", "Hello", "")]
        public void ParseResponse_KnownShapes(string body, string text, string source)
        {
            var parsed = FreeTranslationService.ParseResponse(body);

            Assert.Equal(text, parsed!.Value.Text);
            Assert.Equal(source, parsed.Value.Source);
        }

        [Theory]
        [InlineData("<html>Sorry...</html>")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void ParseResponse_Garbage_ReturnsNull(string body)
        {
            Assert.Null(FreeTranslationService.ParseResponse(body));
        }

        private static HttpResponseMessage Json(string body) => JsonResponse(body);

        private static HttpResponseMessage JsonResponse(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
