using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    public class MauiShareService : IShareService
    {
        private static readonly string[] WhatsAppPackages = { "com.whatsapp", "com.whatsapp.w4b" };

        public Task ShareTextAsync(string title, string text) =>
            Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Subject = title,
                Text = text
            });

        public Task<bool> ShareToWhatsAppAsync(string text)
        {
            // Direct intent at the WhatsApp package: skips the share sheet and keeps the text intact
            // (wa.me URLs get length-limited and mangle some emoji). Falls back to WhatsApp Business.
            foreach (var package in WhatsAppPackages)
            {
                try
                {
                    var intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
                    intent.SetType("text/plain");
                    intent.SetPackage(package);
                    intent.PutExtra(Android.Content.Intent.ExtraText, text);
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    Platform.AppContext.StartActivity(intent);
                    return Task.FromResult(true);
                }
                catch (Android.Content.ActivityNotFoundException)
                {
                    // not installed — try the next one
                }
            }

            return Task.FromResult(false);
        }

        public Task CopyToClipboardAsync(string text) => Clipboard.Default.SetTextAsync(text);
    }
}
