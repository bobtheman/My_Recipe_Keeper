namespace My_Recipe_Keeper.Core.Interfaces
{
    /// <summary>Native confirm dialogs — abstracted so destructive actions (delete) stay testable.</summary>
    public interface IAlertService
    {
        Task<bool> ConfirmAsync(string title, string message, string confirmText = "Delete", string cancelText = "Cancel");
    }
}
