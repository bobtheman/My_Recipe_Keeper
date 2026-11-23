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

            var text = intent.GetStringExtra(Intent.ExtraText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SharedLinks.Inbox.Post(text);
            }
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
