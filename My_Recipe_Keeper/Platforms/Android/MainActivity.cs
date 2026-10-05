using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;
using My_Recipe_Keeper.Services;

namespace My_Recipe_Keeper
{
    // ActionSend text/plain puts "My Recipe Keeper" in the share sheet of Instagram, TikTok, Chrome etc.
    // SingleTask so a share while the app is already open lands in OnNewIntent instead of stacking a
    // second copy of the app on top.
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "text/plain", Label = "Save recipe")]
    // Screenshots / screen recordings of reels whose recipe is on screen, not in the caption → OCR import.
    [IntentFilter(new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "image/*", Label = "Read recipe")]
    [IntentFilter(new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "video/*", Label = "Read recipe")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // targetSdk 35+ forces edge-to-edge — pad the content root by the status bar inset so the
            // BlazorWebView isn't drawn underneath it (same fix as Pok_E_List).
            var rootView = Window?.DecorView.FindViewById(global::Android.Resource.Id.Content);
            if (rootView is not null)
            {
                ViewCompat.SetOnApplyWindowInsetsListener(rootView, new TopInsetPaddingListener());
            }

            // Recreated activities (rotation etc.) replay the original intent — only handle a fresh launch.
            if (savedInstanceState is null)
            {
                HandleShareIntent(Intent);
            }
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleShareIntent(intent);
        }

        private void HandleShareIntent(Intent? intent)
        {
            if (intent?.Action is not (Intent.ActionSend or Intent.ActionSendMultiple))
            {
                return;
            }

            var isMedia = intent.Type is not null &&
                (intent.Type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || intent.Type.StartsWith("video/", StringComparison.OrdinalIgnoreCase));

            if (!isMedia)
            {
                var text = intent.GetStringExtra(Intent.ExtraText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SharedLinks.Inbox.Post(text);
                }

                return;
            }

            var uris = new List<global::Android.Net.Uri>();
            if (intent.Action == Intent.ActionSendMultiple)
            {
#pragma warning disable CA1422 // typed overload is API 33+; minSdk is 24
                var list = intent.GetParcelableArrayListExtra(Intent.ExtraStream);
#pragma warning restore CA1422
                if (list is not null)
                {
                    foreach (var item in list)
                    {
                        if (item is global::Android.Net.Uri uri)
                        {
                            uris.Add(uri);
                        }
                    }
                }
            }
            else
            {
#pragma warning disable CA1422
                if (intent.GetParcelableExtra(Intent.ExtraStream) is global::Android.Net.Uri uri)
#pragma warning restore CA1422
                {
                    uris.Add(uri);
                }
            }

            if (uris.Count == 0)
            {
                return;
            }

            // Screen recordings can be tens of MB — copy off the UI thread, then hand over to Blazor.
            var context = ApplicationContext!;
            _ = Task.Run(() =>
            {
                var paths = Platforms.Android.SharedMediaImporter.CopyToCache(context, uris);
                SharedLinks.Inbox.PostMedia(paths);
            });
        }

        private sealed class TopInsetPaddingListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View v, WindowInsetsCompat insets)
            {
                var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars());
                v.SetPadding(v.PaddingLeft, bars.Top, v.PaddingRight, v.PaddingBottom);
                return insets;
            }
        }
    }
}
