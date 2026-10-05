using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class OverlayTextParserTests
    {
        // OCR lines as ML Kit returns them for the mattaroons Mini Egg Cookie Pie screenshot
        // (instagram.com/p/DWTFtrZBK9N): overlay text plus the mixer timer and sweet packets.
        private static readonly string[] CreamingFrameOcr =
        {
            "Cream together",
            "230g light brown sugar,",
            "110g caster sugar &",
            "200g soft unsalted butter",
            "for couple of mins till pale & fluffy",
            "02:51",
            "RESET",
            "5"
        };

        [Fact]
        public void CleanOcrText_DropsTimerAndFragments()
        {
            var cleaned = OverlayTextParser.CleanOcrText(CreamingFrameOcr);

            Assert.DoesNotContain("02:51", cleaned);
            Assert.StartsWith("Cream together", cleaned);
            Assert.Contains("RESET", cleaned); // words stay — the user removes packaging text on review
        }

        [Fact]
        public void Parse_OverlayFrame_OneStepAndQuantityIngredients()
        {
            var text = string.Join("\n", CreamingFrameOcr.Take(5));

            var result = OverlayTextParser.Parse(text);

            Assert.Equal(new[] { "230g light brown sugar", "110g caster sugar", "200g soft unsalted butter" }, result.Ingredients);
            var step = Assert.Single(result.Instructions);
            Assert.Equal("Cream together 230g light brown sugar, 110g caster sugar & 200g soft unsalted butter for couple of mins till pale & fluffy", step);
        }

        [Fact]
        public void Parse_MultipleScreenshots_EachBlockIsAStepAndDuplicatesDropped()
        {
            const string text = """
                Add 2 large eggs
                & 1 tsp vanilla extract

                Add 2 large eggs
                & 1 tsp vanilla extract

                Fold in 400g plain flour
                then bake at 180C for 25 mins
                """;

            var result = OverlayTextParser.Parse(text);

            Assert.Equal(2, result.Instructions.Count);
            Assert.Equal(new[] { "2 large eggs", "1 tsp vanilla extract", "400g plain flour" }, result.Ingredients);
        }

        [Theory]
        [InlineData("bake at 180C for 25 mins")]
        [InlineData("chill for 2 hours")]
        [InlineData("makes 12 cookies")]
        public void ExtractIngredients_TimesTemperaturesAndYields_Ignored(string text)
        {
            Assert.Empty(OverlayTextParser.ExtractIngredients(text));
        }

        [Theory]
        [InlineData("2 tbsp honey", "2 tbsp honey")]
        [InlineData("½ cup milk", "½ cup milk")]
        [InlineData("1.5kg potatoes", "1.5kg potatoes")]
        [InlineData("3 cloves of garlic", "3 cloves garlic")]
        [InlineData("100g Mini Eggs roughly chopped", "100g Mini Eggs roughly chopped")]
        public void ExtractIngredients_CommonShapes(string text, string expected)
        {
            Assert.Equal(expected, Assert.Single(OverlayTextParser.ExtractIngredients(text)));
        }

        [Fact]
        public void DistinctBlocks_VideoFrames_CollapsesRepeatsAndKeepsFullestReading()
        {
            var frames = new[]
            {
                "Cream together\n230g light brown sugar,",                       // fading in
                "Cream together\n230g light brown sugar,\n110g caster sugar &",  // full
                "Cream together\n230g light brown sugar,\n110g caster sugar &",  // held on screen
                "Add 1 large egg\n& 1 egg yolk",
                "Add 1 large egg\n& 1 egg yo1k",                                 // OCR jitter
                "Cream together\n230g light brown sugar,\n110g caster sugar &"   // exact repeat later
            };

            var blocks = OverlayTextParser.DistinctBlocks(frames);

            Assert.Equal(2, blocks.Count);
            Assert.Contains("110g caster sugar", blocks[0]);
            Assert.StartsWith("Add 1 large egg", blocks[1]);
        }

        [Fact]
        public void DistinctBlocks_BlankFrames_Skipped()
        {
            Assert.Empty(OverlayTextParser.DistinctBlocks(new[] { "", "   ", "\n" }));
        }

        [Fact]
        public void Parse_RecipeCardWithHeadings_UsesCaptionParser()
        {
            var result = OverlayTextParser.Parse("Ingredients:\n2 eggs\n100g flour\nMethod:\nMix\nBake");

            Assert.Equal(new[] { "2 eggs", "100g flour" }, result.Ingredients);
            Assert.Equal(new[] { "Mix", "Bake" }, result.Instructions);
        }
    }
}
