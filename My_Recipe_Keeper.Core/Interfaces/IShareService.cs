namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IShareService
    {
        /// <summary>OS share sheet (WhatsApp, Keep, Messages, email...).</summary>
        Task ShareTextAsync(string title, string text);

        /// <summary>Opens WhatsApp directly with the text pre-filled; returns false if it couldn't be launched.</summary>
        Task<bool> ShareToWhatsAppAsync(string text);

        Task CopyToClipboardAsync(string text);
    }
}
