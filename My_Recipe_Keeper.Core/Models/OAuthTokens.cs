namespace My_Recipe_Keeper.Core.Models
{
    public class OAuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc.AddSeconds(-30);
    }
}
