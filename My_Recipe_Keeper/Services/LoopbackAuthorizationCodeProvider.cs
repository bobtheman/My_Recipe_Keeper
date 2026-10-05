using System.Net;
using System.Text;
using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    /// <summary>
    /// Captures the OAuth redirect via a local loopback listener while the consent page runs in
    /// a Custom Tab (Android) launched through IBrowserLauncher. Once the redirect lands, brings
    /// MainActivity back to the foreground so the Custom Tab is dismissed automatically.
    /// </summary>
    public class LoopbackAuthorizationCodeProvider : IAuthorizationCodeProvider
    {
        private readonly IBrowserLauncher _browserLauncher;

        public LoopbackAuthorizationCodeProvider(IBrowserLauncher browserLauncher)
        {
            _browserLauncher = browserLauncher;
        }

        public async Task<string?> GetAuthorizationCodeAsync(Uri authUrl, Uri redirectUri, CancellationToken ct = default)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri.ToString());
            listener.Start();

            try
            {
                await _browserLauncher.OpenAsync(authUrl);

                using var registration = ct.Register(listener.Stop);
                var context = await listener.GetContextAsync();
                var code = context.Request.QueryString["code"];

                const string responseHtml = "<html><body>Signed in. Returning to My Recipe Keeper...</body></html>";
                var buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, ct);
                context.Response.OutputStream.Close();

                BringAppToForeground();

                return code;
            }
            catch (HttpListenerException)
            {
                // listener.Stop() from a cancellation races GetContextAsync into this.
                return null;
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
            }
        }

        private static void BringAppToForeground()
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity is not null)
            {
                var intent = new Android.Content.Intent(activity, activity.GetType());
                intent.SetFlags(Android.Content.ActivityFlags.ReorderToFront);
                activity.StartActivity(intent);
            }
#endif
        }
    }
}
