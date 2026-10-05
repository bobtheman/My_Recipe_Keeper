using System.Security.Cryptography;
using System.Text;

namespace My_Recipe_Keeper.Core.Services
{
    /// <summary>Pure PKCE verifier/challenge generation. No platform deps — unit testable as-is.</summary>
    public static class PkceHelper
    {
        public static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        public static string GenerateCodeChallenge(string codeVerifier) =>
            Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
