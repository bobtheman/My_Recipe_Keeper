using My_Recipe_Keeper.Core.Services;

namespace My_Recipe_Keeper.Services
{
    /// <summary>
    /// MainActivity receives share intents before (cold start) or outside of DI's reach, so the one
    /// inbox instance is created here and registered into DI as-is — both sides see the same object.
    /// </summary>
    public static class SharedLinks
    {
        public static SharedLinkInbox Inbox { get; } = new();
    }
}
