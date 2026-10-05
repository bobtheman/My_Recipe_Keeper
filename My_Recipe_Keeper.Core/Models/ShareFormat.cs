namespace My_Recipe_Keeper.Core.Models
{
    public enum ShareFormat
    {
        /// <summary>Ingredients only, as a tick-off list — for sending to whoever is doing the shop.</summary>
        ShoppingList,

        /// <summary>Everything: ingredients, method, times, notes.</summary>
        FullRecipe
    }
}
