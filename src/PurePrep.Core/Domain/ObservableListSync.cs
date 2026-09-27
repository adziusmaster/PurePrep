using System.Collections.ObjectModel;

namespace PurePrep.Domain;

/// <summary>
/// Brings a bound <see cref="ObservableCollection{T}"/> in line with a new list using only granular
/// Remove / Move / Insert changes — never Clear(), whose Reset notification makes an Android
/// RecyclerView throw away every row, the list header included. Rebuilding the header that way took
/// keyboard focus out of the Home search box on every keystroke.
/// </summary>
public static class ObservableListSync
{
    public static void SyncTo<T>(this ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        var comparer = EqualityComparer<T>.Default;
        var keep = new HashSet<T>(desired, comparer);

        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(target[i]))
                target.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < target.Count && comparer.Equals(target[i], desired[i]))
                continue;

            var existing = -1;
            for (var j = i + 1; j < target.Count; j++)
            {
                if (comparer.Equals(target[j], desired[i]))
                {
                    existing = j;
                    break;
                }
            }

            if (existing >= 0)
                target.Move(existing, i);
            else
                target.Insert(i, desired[i]);
        }

        // Only reachable with duplicate entries in the target; trim whatever is left over.
        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }
}
