using Unity.Jobs;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal static class IndirectListExtensions
    {
        public static unsafe JobHandle ScheduleWithIndirectList<T, U>(this T jobData, IndirectList<U> list, int innerLoopBatchCount = 1, JobHandle dependencies = default)
            where T : struct, IJobParallelForDefer
            where U : unmanaged
        {
            return jobData.Schedule(&list.List->m_length, innerLoopBatchCount, dependencies);
        }
    }
}
