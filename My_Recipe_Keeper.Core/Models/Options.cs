namespace My_Recipe_Keeper.Core.Models
{
    public class ScraperOptions
    {
        // Desktop browser UA: plenty of recipe sites serve a stripped/blocked page to unknown clients.
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36";

        // Instagram/Facebook only include the post caption (og:description) for link-preview crawlers;
        // a normal browser UA gets the login wall with no caption in it.
        public string SocialUserAgent { get; set; } = "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)";

        public string TikTokOEmbedUrl { get; set; } = "https://www.tiktok.com/oembed";

        public int TimeoutSeconds { get; set; } = 20;
    }

    public class TranslationOptions
    {
        public string Endpoint { get; set; } = "https://clients5.google.com/translate_a/t";

        /// <summary>Language recipes are translated into (ISO 639-1).</summary>
        public string TargetLanguage { get; set; } = "en";
    }

    public class GoogleAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        // appdata scope: backups live in a hidden per-app folder, invisible in the user's normal
        // Drive UI and inaccessible to any other app — narrower than drive.file.
        public string Scope { get; set; } = "https://www.googleapis.com/auth/drive.appdata";
        public int LoopbackPort { get; set; } = 12347;
        public string LoopbackHost { get; set; } = "127.0.0.1";
        public string AuthUrl { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
        public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";
    }

    public class AppSettingsModel
    {
        public bool AutoBackupEnabled { get; set; }
        public string ThemeMode { get; set; } = "System"; // "System" | "Light" | "Dark"
        public bool IncludeSourceLinkWhenSharing { get; set; } = true;
        public bool AutoTranslate { get; set; } = true;
    }
}
