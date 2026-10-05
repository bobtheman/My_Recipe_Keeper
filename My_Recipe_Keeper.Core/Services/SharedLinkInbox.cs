using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Core.Services
{
    public class SharedLinkInbox : ISharedLinkInbox
    {
        private readonly object _lock = new();
        private string? _pending;
        private IReadOnlyList<string> _pendingMedia = Array.Empty<string>();

        public event Action? LinkReceived;

        public bool HasMedia
        {
            get
            {
                lock (_lock)
                {
                    return _pendingMedia.Count > 0;
                }
            }
        }

        public void Post(string sharedText)
        {
            if (string.IsNullOrWhiteSpace(sharedText))
            {
                return;
            }

            lock (_lock)
            {
                _pending = sharedText;
            }

            LinkReceived?.Invoke();
        }

        public void PostMedia(IReadOnlyList<string> mediaPaths)
        {
            if (mediaPaths is null || mediaPaths.Count == 0)
            {
                return;
            }

            lock (_lock)
            {
                _pendingMedia = mediaPaths.ToList();
            }

            LinkReceived?.Invoke();
        }

        public string? Take()
        {
            lock (_lock)
            {
                var value = _pending;
                _pending = null;
                return value;
            }
        }

        public IReadOnlyList<string> TakeMedia()
        {
            lock (_lock)
            {
                var value = _pendingMedia;
                _pendingMedia = Array.Empty<string>();
                return value;
            }
        }
    }
}
