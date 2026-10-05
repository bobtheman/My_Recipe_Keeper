using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Tests.TestSupport
{
    public sealed class FakeSecureTokenStore : ISecureTokenStore
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public void Remove(string key) => _values.Remove(key);
    }
}
