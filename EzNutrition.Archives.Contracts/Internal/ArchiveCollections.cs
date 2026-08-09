using System.Collections.ObjectModel;

namespace EzNutrition.Archives.Contracts.Internal;

internal static class ArchiveCollections
{
    public static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var snapshot = source.ToArray();
        return snapshot.Length == 0
            ? Array.Empty<T>()
            : new ReadOnlyCollection<T>(snapshot);
    }
}
