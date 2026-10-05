using My_Recipe_Keeper.Core.Interfaces;

namespace My_Recipe_Keeper.Services
{
    public class SecureStorageTokenStore : ISecureTokenStore
    {
        public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

        public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

        public void Remove(string key) => SecureStorage.Default.Remove(key);
    }
}
