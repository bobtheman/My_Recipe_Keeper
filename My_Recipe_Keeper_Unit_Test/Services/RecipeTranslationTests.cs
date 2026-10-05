using System.Net;
using System.Net.Http.Headers;
using NSubstitute;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class RecipeTranslationTests
    {
        // Shape of the real canfeezam post (instagram.com/p/DWbnsPDjWl6), trimmed.
        private const string TurkishInstagramPage = """
            <meta property="og:title" content="Nurgül Kotan on Instagram: &quot;Kapalı Hamburger&quot;" />
            <meta property="og:description" content="5M likes, 12K comments - canfeezam on March 28, 2026: &quot;Kapalı Hamburger 🍔&#10;&#10;Hamuru için:&#10;1 su bardağı ılık süt&#10;&#10;Yapılışı:&#10;Hamuru yoğuralım.&#10;&#10;#kapalıburger&quot;." />
            """;

        [Fact]
        public async Task ScrapeAsync_ForeignCaption_TranslatedBeforeParsing()
        {
            var translation = Substitute.For<ITranslationService>();
            translation.TranslateAsync(Arg.Is<string>(s => s.StartsWith("Kapalı Hamburger")), "en", Arg.Any<CancellationToken>())
                .Returns(new TranslationResult
                {
                    SourceLanguage = "tr",
                    Text = "Closed Hamburger 🍔\n\nFor the dough:\n1 glass of warm milk\n\nHow to make:\nKnead the dough.\n\n#kapalıburger"
                });
            var handler = new RoutedHttpMessageHandler().When(HttpMethod.Get, "instagram.com", Html(TurkishInstagramPage));
            var sut = new RecipeScraperService(new HttpClient(handler), new ScraperOptions(), translation);

            var result = await sut.ScrapeAsync("https://www.instagram.com/p/DWbnsPDjWl6/", "en");

            Assert.Equal("tr", result.TranslatedFrom);
            Assert.Equal(ScrapeSource.SocialCaption, result.Source);
            Assert.Equal("Closed Hamburger", result.Recipe!.Title);
            Assert.Equal(new[] { "For the dough:", "1 glass of warm milk" }, result.Recipe.Ingredients);
            Assert.Equal(new[] { "Knead the dough." }, result.Recipe.Instructions);
        }

        [Fact]
        public async Task ScrapeAsync_TranslateToNull_NoTranslationCalls()
        {
            var translation = Substitute.For<ITranslationService>();
            var handler = new RoutedHttpMessageHandler().When(HttpMethod.Get, "instagram.com", Html(TurkishInstagramPage));
            var sut = new RecipeScraperService(new HttpClient(handler), new ScraperOptions(), translation);

            var result = await sut.ScrapeAsync("https://www.instagram.com/p/DWbnsPDjWl6/");

            Assert.Null(result.TranslatedFrom);
            await translation.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
        }

        [Fact]
        public async Task ScrapeAsync_TranslationUnavailable_KeepsOriginalRecipe()
        {
            var translation = Substitute.For<ITranslationService>();
            translation.TranslateAsync(default!, default!, default).ReturnsForAnyArgs((TranslationResult?)null);
            var handler = new RoutedHttpMessageHandler().When(HttpMethod.Get, "instagram.com", Html(TurkishInstagramPage));
            var sut = new RecipeScraperService(new HttpClient(handler), new ScraperOptions(), translation);

            var result = await sut.ScrapeAsync("https://www.instagram.com/p/DWbnsPDjWl6/", "en");

            Assert.True(result.Success);
            Assert.Null(result.TranslatedFrom);
            Assert.Equal("Kapalı Hamburger", result.Recipe!.Title);
        }

        [Fact]
        public async Task RecipeTranslator_EnglishRecipe_StopsAfterFirstField()
        {
            var translation = Substitute.For<ITranslationService>();
            translation.TranslateAsync(default!, default!, default)
                .ReturnsForAnyArgs(call => new TranslationResult { Text = call.ArgAt<string>(0), SourceLanguage = "en" });
            var recipe = new RecipeEntity { Title = "Pancakes", IngredientsText = "2 eggs", InstructionsText = "Mix" };

            var source = await new RecipeTranslator(translation).TranslateAsync(recipe, "en");

            Assert.Null(source);
            await translation.ReceivedWithAnyArgs(1).TranslateAsync(default!, default!, default);
        }

        [Fact]
        public async Task RecipeTranslator_ForeignRecipe_TranslatesEveryField()
        {
            var translation = Substitute.For<ITranslationService>();
            translation.TranslateAsync(default!, default!, default)
                .ReturnsForAnyArgs(call => new TranslationResult { Text = "EN:" + call.ArgAt<string>(0), SourceLanguage = "it" });
            var recipe = new RecipeEntity { Title = "Crostata", IngredientsText = "2 uova", InstructionsText = "Mescolare", Notes = "Buono" };

            var source = await new RecipeTranslator(translation).TranslateAsync(recipe, "en");

            Assert.Equal("it", source);
            Assert.Equal("EN:Crostata", recipe.Title);
            Assert.Equal("EN:2 uova", recipe.IngredientsText);
            Assert.Equal("EN:Mescolare", recipe.InstructionsText);
            Assert.Equal("EN:Buono", recipe.Notes);
        }

        private static HttpResponseMessage Html(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
    }
}
