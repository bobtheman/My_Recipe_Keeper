using Microsoft.JSInterop;

namespace My_Recipe_Keeper.Components.Layout
{
    public partial class MainLayout
    {
        protected override void OnInitialized()
        {
            SharedLinkInbox.LinkReceived += OnLinkReceived;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var settings = await SettingsService.LoadAsync();
                await JsRuntime.InvokeVoidAsync("recipeKeeper.applyTheme", settings.ThemeMode);

                // Cold start from a share: the intent was posted before this layout existed.
                OpenPendingSharedLink();
            }
        }

        // Raised on the Android UI thread by MainActivity — marshal onto the renderer's context.
        private void OnLinkReceived() => _ = InvokeAsync(OpenPendingSharedLink);

        private void OpenPendingSharedLink()
        {
            var shared = SharedLinkInbox.Take();
            if (shared is not null)
            {
                Nav.NavigateTo($"/recipe/add?shared={Uri.EscapeDataString(shared)}");
                return;
            }

            // Media stays in the inbox; the editor takes it. The nonce makes a repeat share re-trigger.
            if (SharedLinkInbox.HasMedia)
            {
                Nav.NavigateTo($"/recipe/add?media={DateTime.UtcNow.Ticks}");
            }
        }

        public void Dispose()
        {
            SharedLinkInbox.LinkReceived -= OnLinkReceived;
        }
    }
}
