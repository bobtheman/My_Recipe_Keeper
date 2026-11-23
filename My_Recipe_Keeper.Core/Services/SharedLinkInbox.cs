using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Core.Services
{
    public class SharedLinkInbox : ISharedLinkInbox
    {
        private readonly object _lock = new();
        private string? _pending;

        public event Action? LinkReceived;

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

        public string? Take()
        {
            lock (_lock)
            {
                var value = _pending;
                _pending = null;
                return value;
            }
        }
    }
}
