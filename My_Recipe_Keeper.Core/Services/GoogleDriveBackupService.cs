using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using My_Recipe_Keeper.Core.Interfaces;
using My_Recipe_Keeper.Core.Models;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>
    /// Talks to the Drive v3 REST API directly over the shared typed HttpClient instead of the
    /// Google.Apis.Drive.v3 SDK — fewer allocations/reflection at startup, and it's the thing that
    /// was making cold start on the old app noticeably slow.
    /// Backups go to the app's own "appDataFolder" (drive.file scope), invisible in the user's
    /// normal Drive UI and inaccessible to any other app.
    /// </summary>
    public class GoogleDriveBackupService : IGoogleBackupService
    {
        private const string TokenStorageKey = "google_tokens";
        private const string BackupNamePrefix = "recipekeeper-";
        private const int SchemaVersion = 1;

        private readonly HttpClient _httpClient;
        private readonly GoogleAuthOptions _options;
        private readonly IRecipeRepository _recipeRepository;
        private readonly IAppPaths _paths;
        private readonly ISecureTokenStore _tokenStore;
        private readonly IAuthorizationCodeProvider _codeProvider;

        public GoogleDriveBackupService(
            HttpClient httpClient,
            GoogleAuthOptions options,
            IRecipeRepository recipeRepository,
            IAppPaths paths,
            ISecureTokenStore tokenStore,
            IAuthorizationCodeProvider codeProvider)
        {
            _httpClient = httpClient;
            _options = options;
            _recipeRepository = recipeRepository;
            _paths = paths;
            _tokenStore = tokenStore;
            _codeProvider = codeProvider;
        }

        public async Task<bool> IsSignedInAsync() => await LoadTokensAsync() is not null;

        public async Task<bool> SignInAsync(CancellationToken ct = default)
        {
            var verifier = PkceHelper.GenerateCodeVerifier();
            var challenge = PkceHelper.GenerateCodeChallenge(verifier);
            var redirectUri = new Uri($"http://{_options.LoopbackHost}:{_options.LoopbackPort}/");
            var authUrl = new Uri(BuildAuthUrl(challenge, redirectUri.ToString()));

            var code = await _codeProvider.GetAuthorizationCodeAsync(authUrl, redirectUri, ct);
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            var tokens = await ExchangeCodeForTokensAsync(code, verifier, redirectUri.ToString(), ct);
            if (tokens is null)
            {
                return false;
            }

            await SaveTokensAsync(tokens);
            return true;
        }

        public Task SignOutAsync()
        {
            _tokenStore.Remove(TokenStorageKey);
            return Task.CompletedTask;
        }

        public async Task<bool> BackupAsync(CancellationToken ct = default)
        {
            var accessToken = await GetValidAccessTokenAsync(ct)
                ?? throw new InvalidOperationException("Not signed in to Google Drive.");

            var folderId = await EnsureBackupFolderAsync(accessToken, ct);

            // Flush and release the SQLite handle so the file we upload is a complete,
            // checkpointed database rather than one with pending WAL content.
            await _recipeRepository.CloseAsync();
            string? uploadedId = null;
            try
            {
                var fileName = $"{BackupNamePrefix}v{SchemaVersion}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db3";
                await using var stream = File.OpenRead(_paths.DatabasePath);
                uploadedId = await UploadFileAsync(accessToken, fileName, folderId, stream, ct);
            }
            finally
            {
                await _recipeRepository.ReopenAsync();
            }

            if (uploadedId is null)
            {
                return false;
            }

            await DeleteOldBackupsAsync(accessToken, folderId, uploadedId, ct);
            return true;
        }

        public async Task<bool> RestoreAsync(CancellationToken ct = default)
        {
            var accessToken = await GetValidAccessTokenAsync(ct)
                ?? throw new InvalidOperationException("Not signed in to Google Drive.");

            var folderId = await EnsureBackupFolderAsync(accessToken, ct);
            var latest = await FindLatestBackupAsync(accessToken, folderId, ct);
            if (latest is null)
            {
                return false;
            }

            await _recipeRepository.CloseAsync();
            try
            {
                var bytes = await DownloadFileAsync(accessToken, latest.Id, ct);
                await File.WriteAllBytesAsync(_paths.DatabasePath, bytes, ct);
            }
            finally
            {
                await _recipeRepository.ReopenAsync();
            }

            return true;
        }

        private async Task<string?> GetValidAccessTokenAsync(CancellationToken ct)
        {
            var tokens = await LoadTokensAsync();
            if (tokens is null)
            {
                return null;
            }

            if (!tokens.IsExpired)
            {
                return tokens.AccessToken;
            }

            if (string.IsNullOrEmpty(tokens.RefreshToken))
            {
                return null;
            }

            var refreshed = await RefreshTokensAsync(tokens.RefreshToken, ct);
            if (refreshed is null)
            {
                return null;
            }

            await SaveTokensAsync(refreshed);
            return refreshed.AccessToken;
        }

        private string BuildAuthUrl(string challenge, string redirectUri) =>
            _options.AuthUrl +
            $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(_options.Scope)}" +
            "&access_type=offline&prompt=consent" +
            $"&code_challenge={challenge}&code_challenge_method=S256";

        private async Task<OAuthTokens?> ExchangeCodeForTokensAsync(string code, string verifier, string redirectUri, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            };

            using var response = await _httpClient.PostAsync(_options.TokenUrl, new FormUrlEncodedContent(form), ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (body is null)
            {
                return null;
            }

            return new OAuthTokens
            {
                AccessToken = body.AccessToken,
                RefreshToken = body.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(body.ExpiresIn)
            };
        }

        private async Task<OAuthTokens?> RefreshTokensAsync(string refreshToken, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "refresh_token"
            };

            using var response = await _httpClient.PostAsync(_options.TokenUrl, new FormUrlEncodedContent(form), ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (body is null)
            {
                return null;
            }

            return new OAuthTokens
            {
                AccessToken = body.AccessToken,
                RefreshToken = refreshToken, // refresh grant does not return a new refresh token
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(body.ExpiresIn)
            };
        }

        private async Task<OAuthTokens?> LoadTokensAsync()
        {
            var json = await _tokenStore.GetAsync(TokenStorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OAuthTokens>(json);
        }

        private Task SaveTokensAsync(OAuthTokens tokens) =>
            _tokenStore.SetAsync(TokenStorageKey, JsonSerializer.Serialize(tokens));

        private async Task<string> EnsureBackupFolderAsync(string accessToken, CancellationToken ct)
        {
            const string query = "mimeType = 'application/vnd.google-apps.folder' and name = 'MyRecipeKeeper' and trashed = false and 'appDataFolder' in parents";
            var existing = await ListFilesAsync(accessToken, query, ct);
            if (existing.Count > 0)
            {
                return existing[0].Id;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files")
            {
                Content = JsonContent.Create(new
                {
                    name = "MyRecipeKeeper",
                    mimeType = "application/vnd.google-apps.folder",
                    parents = new[] { "appDataFolder" }
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: ct);
            return created!.Id;
        }

        private async Task<List<DriveFile>> ListFilesAsync(string accessToken, string query, CancellationToken ct)
        {
            var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}" +
                      "&spaces=appDataFolder&fields=files(id,name,modifiedTime)&orderBy=modifiedTime desc";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<DriveFileList>(cancellationToken: ct);
            return body?.Files ?? new List<DriveFile>();
        }

        private async Task<DriveFile?> FindLatestBackupAsync(string accessToken, string folderId, CancellationToken ct)
        {
            var query = $"'{folderId}' in parents and name contains '{BackupNamePrefix}' and trashed = false";
            var files = await ListFilesAsync(accessToken, query, ct);
            return files.Count > 0 ? files[0] : null;
        }

        private async Task<string?> UploadFileAsync(string accessToken, string fileName, string folderId, Stream content, CancellationToken ct)
        {
            var metadata = new { name = fileName, parents = new[] { folderId } };
            using var multipart = new MultipartContent("related");
            multipart.Add(JsonContent.Create(metadata));

            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(fileContent);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id")
            {
                Content = multipart
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<DriveFile>(cancellationToken: ct);
            return body?.Id;
        }

        private async Task<byte[]> DownloadFileAsync(string accessToken, string fileId, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&spaces=appDataFolder");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }

        private async Task DeleteOldBackupsAsync(string accessToken, string folderId, string keepFileId, CancellationToken ct)
        {
            var query = $"'{folderId}' in parents and name contains '{BackupNamePrefix}' and trashed = false";
            var files = await ListFilesAsync(accessToken, query, ct);

            foreach (var file in files.Where(f => f.Id != keepFileId))
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/drive/v3/files/{file.Id}?spaces=appDataFolder");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
            }
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private class DriveFile
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private class DriveFileList
        {
            [JsonPropertyName("files")]
            public List<DriveFile> Files { get; set; } = new();
        }
    }
}
