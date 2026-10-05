namespace My_Recipe_Keeper.Core.Interfaces
{
    /// <summary>
    /// Hand-off point between Android share intents (arrive on the Activity, possibly before Blazor
    /// has rendered) and the Blazor UI. Holds the latest shared link or screenshots until the UI takes them.
    /// </summary>
    public interface ISharedLinkInbox
    {
        event Action? LinkReceived;

        void Post(string sharedText);

        /// <summary>Screenshots / screen recordings shared to the app (already copied to local files) for OCR import.</summary>
        void PostMedia(IReadOnlyList<string> mediaPaths);

        bool HasMedia { get; }

        /// <summary>Returns and clears the pending shared text, or null if none.</summary>
        string? Take();

        /// <summary>Returns and clears the pending shared media paths (empty if none).</summary>
        IReadOnlyList<string> TakeMedia();
    }
}
