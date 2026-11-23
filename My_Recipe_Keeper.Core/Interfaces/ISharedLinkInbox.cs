namespace My_Recipe_Keeper.Core.Interfaces
{
    /// <summary>
    /// Hand-off point between Android share intents (arrive on the Activity, possibly before Blazor
    /// has rendered) and the Blazor UI. Holds the latest shared link until the UI takes them.
    /// </summary>
    public interface ISharedLinkInbox
    {
        event Action? LinkReceived;

        void Post(string sharedText);

        /// <summary>Returns and clears the pending shared text, or null if none.</summary>
        string? Take();
    }
}
