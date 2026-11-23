using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Tests.Services
{
    public class SharedLinkInboxTests
    {
        [Fact]
        public void Post_RaisesEventAndTakeClearsPending()
        {
            var sut = new SharedLinkInbox();
            var raised = 0;
            sut.LinkReceived += () => raised++;

            sut.Post("https://example.com");

            Assert.Equal(1, raised);
            Assert.Equal("https://example.com", sut.Take());
            Assert.Null(sut.Take());
        }

        [Fact]
        public void Post_BlankText_Ignored()
        {
            var sut = new SharedLinkInbox();
            var raised = false;
            sut.LinkReceived += () => raised = true;

            sut.Post("  ");

            Assert.False(raised);
            Assert.Null(sut.Take());
        }
    }
}
