using SQLite;

namespace My_Recipe_Keeper.Core.Models
{
    /// <summary>
    /// Ingredients/instructions are stored one-per-line rather than as child tables: the editor is a
    /// plain textarea per list, and a line is the natural unit for both editing and sharing.
    /// </summary>
    [Table("Recipes")]
    public class RecipeEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string IngredientsText { get; set; } = string.Empty;

        public string InstructionsText { get; set; } = string.Empty;

        public string? Servings { get; set; }

        public string? PrepTime { get; set; }

        public string? CookTime { get; set; }

        public string? TotalTime { get; set; }

        public string? ImageUrl { get; set; }

        [Indexed]
        public string? SourceUrl { get; set; }

        public string? Category { get; set; }

        public string? Notes { get; set; }

        public bool IsFavorite { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public DateTime DateModified { get; set; } = DateTime.UtcNow;

        [Ignore]
        public IReadOnlyList<string> Ingredients => SplitLines(IngredientsText);

        [Ignore]
        public IReadOnlyList<string> Instructions => SplitLines(InstructionsText);

        public static string JoinLines(IEnumerable<string> lines) =>
            string.Join("\n", lines.Select(l => l.Trim()).Where(l => l.Length > 0));

        private static IReadOnlyList<string> SplitLines(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }
}
