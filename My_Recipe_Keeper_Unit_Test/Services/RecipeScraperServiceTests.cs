using System.Net;
using System.Net.Http.Headers;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class RecipeScraperServiceTests
    {
        private static (RecipeScraperService Sut, RoutedHttpMessageHandler Handler) Build()
        {
            var handler = new RoutedHttpMessageHandler();
            return (new RecipeScraperService(new HttpClient(handler), new ScraperOptions()), handler);
        }

        private const string JsonLdPage = """
            <html><head>
            <script type="application/ld+json">{"@context":"https://schema.org","@type":"WebSite","name":"Site"}</script>
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[
              {"@type":"WebPage","name":"Page"},
              {"@type":["Recipe","NewsArticle"],
               "name":"Classic Pancakes &amp; Syrup",
               "description":"<p>Fluffy pancakes</p>",
               "recipeYield":["4","4 pancakes stacks"],
               "prepTime":"PT10M","cookTime":"PT1H5M",
               "image":[{"@type":"ImageObject","url":"https://img.example/p.jpg"}],
               "recipeCategory":["Breakfast"],
               "recipeIngredient":["100g flour","2 eggs","300ml milk"],
               "recipeInstructions":[
                 {"@type":"HowToSection","name":"Batter","itemListElement":[
                   {"@type":"HowToStep","text":"Whisk everything."}]},
                 {"@type":"HowToStep","text":"Fry in a hot pan."}]}
            ]}
            </script></head><body></body></html>
            """;

        [Fact]
        public void ParseHtml_JsonLdGraphRecipe_MapsAllFields()
        {
            var result = RecipeScraperService.ParseHtml(JsonLdPage, "https://example.com/pancakes");

            Assert.True(result.Success);
            Assert.Equal(ScrapeSource.StructuredData, result.Source);
            var recipe = result.Recipe!;
            Assert.Equal("Classic Pancakes & Syrup", recipe.Title);
            Assert.Equal("Fluffy pancakes", recipe.Description);
            Assert.Equal(new[] { "100g flour", "2 eggs", "300ml milk" }, recipe.Ingredients);
            Assert.Equal(new[] { "Batter:", "Whisk everything.", "Fry in a hot pan." }, recipe.Instructions);
            Assert.Equal("4 pancakes stacks", recipe.Servings);
            Assert.Equal("10 mins", recipe.PrepTime);
            Assert.Equal("1 hr 5 mins", recipe.CookTime);
            Assert.Equal("https://img.example/p.jpg", recipe.ImageUrl);
            Assert.Equal("Breakfast", recipe.Category);
            Assert.Equal("https://example.com/pancakes", recipe.SourceUrl);
        }

        [Fact]
        public void ParseHtml_InstructionsAsSingleHtmlString_SplitIntoSteps()
        {
            const string html = """
                <script type="application/ld+json">
                {"@type":"Recipe","name":"Toast","recipeYield":2,"recipeIngredient":"1 slice bread",
                 "recipeInstructions":"<p>Toast the bread.</p><p>Butter it.</p>"}
                </script>
                """;

            var recipe = RecipeScraperService.ParseHtml(html, "https://x.test").Recipe!;

            Assert.Equal(new[] { "Toast the bread.", "Butter it." }, recipe.Instructions);
            Assert.Equal("2 servings", recipe.Servings);
            Assert.Equal(new[] { "1 slice bread" }, recipe.Ingredients);
        }

        [Fact]
        public void ParseHtml_MalformedJsonLd_FallsBackToMetaTags()
        {
            const string html = """
                <html><head>
                <script type="application/ld+json">{ not json </script>
                <meta property="og:title" content="Nice Soup" />
                <meta property="og:image" content="https://img.example/soup.jpg" />
                <meta name="description" content="A warming soup." />
                </head></html>
                """;

            var result = RecipeScraperService.ParseHtml(html, "https://x.test");

            Assert.True(result.Success);
            Assert.Equal(ScrapeSource.PageMetadata, result.Source);
            Assert.Equal("Nice Soup", result.Recipe!.Title);
            Assert.Equal("A warming soup.", result.Recipe.Description);
            Assert.Equal("https://img.example/soup.jpg", result.Recipe.ImageUrl);
        }

        [Fact]
        public void ParseHtml_InstagramCaption_ParsedFromOgDescription()
        {
            const string html = """
                <meta property="og:title" content="Chef Bob on Instagram: &quot;Brownies&quot;" />
                <meta property="og:description" content="10 likes, 2 comments - chefbob on May 1, 2025: &quot;Best Brownies&#10;Ingredients:&#10;2 eggs&#10;100g chocolate&#10;Method:&#10;Mix and bake&quot;." />
                """;

            var result = RecipeScraperService.ParseHtml(html, "https://www.instagram.com/p/abc/");

            Assert.Equal(ScrapeSource.SocialCaption, result.Source);
            Assert.Equal("Best Brownies", result.Recipe!.Title);
            Assert.Equal(new[] { "2 eggs", "100g chocolate" }, result.Recipe.Ingredients);
            Assert.Equal(new[] { "Mix and bake" }, result.Recipe.Instructions);
        }

        [Fact]
        public void ParseHtml_InstagramTruncatedTitleAndCtaCaption_CleanTitleAndDescription()
        {
            // Real shape of rasamalaysia's post (instagram.com/p/DTUOW3CCDvC): og:title cut off by Instagram.
            const string html = """
                <meta property="og:title" content="Bee | Rasa Malaysia on Instagram: &quot;CHINESE BBQ PORK - Sticky, flavorful char siu-marinated pork roasted to perfection. E…" />
                <meta property="og:description" content="43 likes, 2 comments - rasamalaysia on January 9, 2026: &quot;CHINESE BBQ PORK - Sticky, flavorful char siu-marinated pork roasted to perfection. Easy, authentic, and tastes just like your favorite Chinese restaurants. 🤤&#10;&#10;Recipe link in bio or search on Rasa Malaysia! &#10;&#10;For more recipes, check out my link in bio ❤️&#10;&#10;#chinesebbqpork #bbqpork #linkinbio&quot;." />
                """;

            var recipe = RecipeScraperService.ParseHtml(html, "https://www.instagram.com/p/DTUOW3CCDvC/").Recipe!;

            Assert.Equal("CHINESE BBQ PORK", recipe.Title);
            Assert.StartsWith("CHINESE BBQ PORK - Sticky", recipe.Description);
            Assert.DoesNotContain("link in bio", recipe.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#", recipe.Description);
        }

        [Fact]
        public void ParseHtml_NoRecipeCaption_DescriptionDoesNotRepeatTitle()
        {
            const string html = """
                <meta property="og:title" content="Mattaroons on Instagram: &quot;Mini Egg Cookie Pie Recipe" />
                <meta property="og:description" content="5 likes, 1 comments - mattaroons on March 24, 2026: &quot;Mini Egg Cookie Pie Recipe 🍪&#10;&#10;A brand new recipe for Easter!&#10;&#10;#miniegg&#10;&#10;Made with @morphyrichardsuk MixStar&quot;." />
                """;

            var recipe = RecipeScraperService.ParseHtml(html, "https://www.instagram.com/p/DWTFtrZBK9N/").Recipe!;

            Assert.Equal("Mini Egg Cookie Pie Recipe", recipe.Title);
            Assert.Equal("A brand new recipe for Easter!\nMade with morphyrichardsuk MixStar", recipe.Description);
        }

        [Fact]
        public void ParseHtml_NothingUseful_Fails()
        {
            var result = RecipeScraperService.ParseHtml("<html><body>hi</body></html>", "https://x.test");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ScrapeAsync_NoUrlInText_FailsWithoutCallingNetwork()
        {
            var (sut, handler) = Build();

            var result = await sut.ScrapeAsync("just some words");

            Assert.False(result.Success);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task ScrapeAsync_SharedTextWithUrl_FetchesPageWithBrowserUserAgent()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Get, "example.com/pancakes", Html(JsonLdPage));

            var result = await sut.ScrapeAsync("Try this! https://example.com/pancakes");

            Assert.True(result.Success);
            Assert.Equal("Classic Pancakes & Syrup", result.Recipe!.Title);
            Assert.Contains("Mozilla", handler.Requests.Single().Headers.UserAgent.ToString());
        }

        [Fact]
        public async Task ScrapeAsync_InstagramLink_UsesLinkPreviewUserAgent()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Get, "instagram.com", Html("""<meta property="og:title" content="x" />"""));

            await sut.ScrapeAsync("https://www.instagram.com/reel/abc/");

            Assert.Contains("facebookexternalhit", handler.Requests.Single().Headers.UserAgent.ToString());
        }

        [Fact]
        public async Task ScrapeAsync_TikTokLink_UsesOEmbedCaption()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Get, "tiktok.com/oembed", Json(
                """{"title":"Crispy Potatoes\nIngredients:\n1kg potatoes\n2 tbsp oil\nMethod:\nRoast 40 mins","author_name":"spud","thumbnail_url":"https://img.tt/p.jpg"}"""));

            var result = await sut.ScrapeAsync("https://www.tiktok.com/@spud/video/123");

            Assert.Equal(ScrapeSource.SocialCaption, result.Source);
            Assert.Equal("Crispy Potatoes", result.Recipe!.Title);
            Assert.Equal(new[] { "1kg potatoes", "2 tbsp oil" }, result.Recipe.Ingredients);
            Assert.Equal("https://img.tt/p.jpg", result.Recipe.ImageUrl);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task ScrapeAsync_HttpError_ReturnsFailure()
        {
            var (sut, handler) = Build();
            handler.When(HttpMethod.Get, "example.com", new HttpResponseMessage(HttpStatusCode.Forbidden));

            var result = await sut.ScrapeAsync("https://example.com/blocked");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        [Theory]
        [InlineData("PT30M", "30 mins")]
        [InlineData("PT1H", "1 hr")]
        [InlineData("PT2H15M", "2 hrs 15 mins")]
        [InlineData("P0DT0H45M", "45 mins")]
        [InlineData("PT0M", null)]
        [InlineData("about 20 mins", "about 20 mins")]
        public void FormatDuration_ConvertsIso8601(string input, string? expected)
        {
            Assert.Equal(expected, RecipeScraperService.FormatDuration(input));
        }

        private static HttpResponseMessage Html(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        private static HttpResponseMessage Json(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
