namespace My_Recipe_Keeper.Core.Interfaces
{
    public interface IImagePickerService
    {
        /// <summary>Lets the user pick screenshots and/or screen recordings; returns local file paths (empty if cancelled).</summary>
        Task<IReadOnlyList<string>> PickMediaAsync();
    }
}
