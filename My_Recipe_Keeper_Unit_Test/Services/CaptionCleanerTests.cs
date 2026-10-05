using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class CaptionCleanerTests
    {
        [Theory]
        [InlineData("Comment \"CHAR SIU\" and I'll send you the link!")]
        [InlineData("Comment RECIPE below and I’ll DM you the recipe")]
        [InlineData("Follow me @hermanathome for more easy recipes")]
        [InlineData("Recipe link in bio or search on Rasa Malaysia!")]
        [InlineData("For more recipes, check out my link in bio ❤️")]
        [InlineData("Full recipe on my website")]
        [InlineData("Save this for later! 📌")]
        [InlineData("Tag a friend who needs this")]
        [InlineData("#charsiu #chineserecipes #easymeals")]
        [InlineData("@hermanathome")]
        public void CleanLine_NoiseLines_Dropped(string line)
        {
            Assert.Null(CaptionCleaner.CleanLine(line));
        }

        [Fact]
        public void CleanLine_InlineHashtagsRemoved_TextKept()
        {
            Assert.Equal("Sticky char siu pork 🤤", CaptionCleaner.CleanLine("Sticky #charsiu char siu pork 🤤 #chineserecipes"));
        }

        [Fact]
        public void CleanLine_MentionInsideSentence_KeepsWordWithoutAt()
        {
            Assert.Equal("Made with morphyrichardsuk MixStar", CaptionCleaner.CleanLine("Made with @morphyrichardsuk MixStar"));
        }

        [Theory]
        [InlineData("2 tbsp soy sauce")]
        [InlineData("Share between 4 bowls")] // "share" only as a CTA when "share this/with"
        [InlineData("Serve with rice & greens")]
        public void CleanLine_RecipeLines_Kept(string line)
        {
            Assert.Equal(line, CaptionCleaner.CleanLine(line));
        }

        [Fact]
        public void CleanBlock_RealRasaMalaysiaCaption_KeepsOnlyDescription()
        {
            const string caption = """
                CHINESE BBQ PORK - Sticky, flavorful char siu-marinated pork roasted to perfection. 🤤

                Recipe link in bio or search on Rasa Malaysia!

                For more recipes, check out my link in bio ❤️

                #chinesebbqpork #bbqpork #homemade #chineserecipes
                """;

            var cleaned = CaptionCleaner.CleanBlock(caption);

            Assert.Equal("CHINESE BBQ PORK - Sticky, flavorful char siu-marinated pork roasted to perfection. 🤤", cleaned);
        }

        [Fact]
        public void Parse_CaptionWithCallToActionInsideRecipe_NoiseNotInSteps()
        {
            const string caption = """
                Char Siu
                Ingredients:
                500g pork shoulder
                Method:
                Roast for 40 mins
                Comment "PORK" and I'll send you the link to the full recipe
                Follow me @hermanathome
                #charsiu #chineserecipes #easymeals
                """;

            var result = RecipeTextParser.Parse(caption);

            Assert.Equal(new[] { "Roast for 40 mins" }, result.Instructions);
        }
    }
}
