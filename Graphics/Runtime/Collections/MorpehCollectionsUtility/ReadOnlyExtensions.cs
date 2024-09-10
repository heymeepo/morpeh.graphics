using Scellecs.Morpeh.Collections;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal static class ReadOnlyExtensions
    {
        public static ReadOnlyIntFastList AsReadOnly(this FastList<int> list) => new ReadOnlyIntFastList(list.data, list.length);

        public static ReadOnlyIntHashSet AsReadOnly(this IntHashSet hashset) => new ReadOnlyIntHashSet(hashset);
    }
}
