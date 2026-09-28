using System.Windows.Media;

namespace Clipalka.App.Models;

public sealed record RecentClipItem(
    string FilePath,
    string Title,
    string SavedAt,
    string Duration,
    ImageSource Thumbnail);
