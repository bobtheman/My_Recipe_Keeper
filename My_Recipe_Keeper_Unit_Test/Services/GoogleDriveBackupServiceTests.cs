using System.Net;
using System.Net.Http.Headers;
using NSubstitute;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;
using My_Recipe_Keeper.Core.Services;
using My_Recipe_Keeper.Tests.TestSupport;

namespace My_Recipe_Keeper.Tests.Services
{
    public class GoogleDriveBackupServiceTests : IDisposable
    {
        private readonly FakeAppPaths _paths = new();
        private readonly FakeSecureTokenStore _tokenStore = new();
        private readonly IRecipeRepository _recipeRepository = Substitute.For<IRecipeRepository>();
        private readonly IAuthorizationCodeProvider _codeProvider = Substitute.For<IAuthorizationCodeProvider>();
        private readonly GoogleAuthOptions _options = new()
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            TokenUrl = "https://oauth2.googleapis.com/token",
            AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth",
            LoopbackHost = "127.0.0.1",
            LoopbackPort = 12346
        };

        public GoogleDriveBackupServiceTests()
        {
            // BackupAsync writes/reads the real db file, so it needs to exist.
            File.WriteAllText(_paths.DatabasePath, "fake-db-contents");
        }

        public void Dispose() => _paths.Dispose();

        private GoogleDriveBackupService Build(RoutedHttpMessageHandler handler) =>
            new(new HttpClient(handler), _options, _recipeRepository, _paths, _tokenStore, _codeProvider);

        [Fact]
        public async Task IsSignedInAsync_NoStoredTokens_ReturnsFalse()
        {
            var sut = Build(new RoutedHttpMessageHandler());

            Assert.False(await sut.IsSignedInAsync());
        }

        [Fact]
        public async Task SignInAsync_UserCancelsConsent_ReturnsFalseAndStoresNothing()
        {
            _codeProvider.GetAuthorizationCodeAsync(Arg.Any<Uri>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
                .Returns((string?)null);
            var sut = Build(new RoutedHttpMessageHandler());

            var result = await sut.SignInAsync();

            Assert.False(result);
            Assert.False(await sut.IsSignedInAsync());
        }

        [Fact]
        public async Task SignInAsync_Success_StoresTokensAndReportsSignedIn()
        {
            _codeProvider.GetAuthorizationCodeAsync(Arg.Any<Uri>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
                .Returns("auth-code");
            var handler = new RoutedHttpMessageHandler()
                .When(HttpMethod.Post, "oauth2.googleapis.com/token", Json(
                    """{"access_token":"abc123","refresh_token":"refresh-xyz","expires_in":3600}"""));
            var sut = Build(handler);

            var result = await sut.SignInAsync();

            Assert.True(result);
            Assert.True(await sut.IsSignedInAsync());
        }

        [Fact]
        public async Task BackupAsync_NotSignedIn_Throws()
        {
            var sut = Build(new RoutedHttpMessageHandler());

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.BackupAsync());
        }

        [Fact]
        public async Task BackupAsync_ClosesAndReopensRepositoryAroundUpload()
        {
            await SignInAsync();

            var handler = new RoutedHttpMessageHandler()
                .When(HttpMethod.Get, "www.googleapis.com/drive/v3/files", Json("""{"files":[]}"""))
                .When(HttpMethod.Post, "www.googleapis.com/drive/v3/files", Json("""{"id":"folder-1","name":"MyRecipeKeeper"}"""))
                .When(HttpMethod.Post, "upload/drive/v3/files", Json("""{"id":"file-1","name":"recipekeeper-v1.db3"}"""));
            var sut = Build(handler);

            var result = await sut.BackupAsync();

            Assert.True(result);
            Received.InOrder(() =>
            {
                _recipeRepository.CloseAsync();
                _recipeRepository.ReopenAsync();
            });
        }

        [Fact]
        public async Task BackupAsync_ReopensRepositoryEvenIfUploadFails()
        {
            await SignInAsync();

            var handler = new RoutedHttpMessageHandler()
                .When(HttpMethod.Get, "www.googleapis.com/drive/v3/files", Json("""{"files":[]}"""))
                .When(HttpMethod.Post, "www.googleapis.com/drive/v3/files", Json("""{"id":"folder-1","name":"MyRecipeKeeper"}"""))
                .When(HttpMethod.Post, "upload/drive/v3/files", new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var sut = Build(handler);

            var result = await sut.BackupAsync();

            Assert.False(result);
            await _recipeRepository.Received(1).ReopenAsync();
        }

        [Fact]
        public async Task RestoreAsync_NoBackupExists_ReturnsFalseWithoutTouchingLocalDb()
        {
            await SignInAsync();

            var handler = new RoutedHttpMessageHandler()
                .When(HttpMethod.Get, "www.googleapis.com/drive/v3/files", Json("""{"files":[]}"""))
                .When(HttpMethod.Post, "www.googleapis.com/drive/v3/files", Json("""{"id":"folder-1","name":"MyRecipeKeeper"}"""));
            var sut = Build(handler);

            var result = await sut.RestoreAsync();

            Assert.False(result);
            await _recipeRepository.DidNotReceive().CloseAsync();
        }

        [Fact]
        public async Task SignOutAsync_RemovesStoredTokens()
        {
            await SignInAsync();

            await Build(new RoutedHttpMessageHandler()).SignOutAsync();

            Assert.False(await Build(new RoutedHttpMessageHandler()).IsSignedInAsync());
        }

        private async Task SignInAsync()
        {
            _codeProvider.GetAuthorizationCodeAsync(Arg.Any<Uri>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>())
                .Returns("auth-code");
            var handler = new RoutedHttpMessageHandler()
                .When(HttpMethod.Post, "oauth2.googleapis.com/token", Json(
                    """{"access_token":"abc123","refresh_token":"refresh-xyz","expires_in":3600}"""));
            await Build(handler).SignInAsync();
        }

        private static HttpResponseMessage Json(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }
    }
}
