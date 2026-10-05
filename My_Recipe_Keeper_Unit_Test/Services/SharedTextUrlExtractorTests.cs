using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class SharedTextUrlExtractorTests
    {
        [Theory]
        [InlineData("https://www.bbcgoodfood.com/recipes/pancakes", "https://www.bbcgoodfood.com/recipes/pancakes")]
        [InlineData("Check this out! https://www.instagram.com/reel/abc/?igsh=xyz", "https://www.instagram.com/reel/abc/?igsh=xyz")]
        [InlineData("Look (https://example.com/r/1).", "https://example.com/r/1")]
        [InlineData("bbcgoodfood.com/recipes/easy-pancakes", "https://bbcgoodfood.com/recipes/easy-pancakes")]
        public void Extract_FindsUrl(string input, string expected)
        {
            Assert.Equal(expected, SharedTextUrlExtractor.Extract(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("no link here")]
        public void Extract_NoUrl_ReturnsNull(string? input)
        {
            Assert.Null(SharedTextUrlExtractor.Extract(input));
        }
    }
}
