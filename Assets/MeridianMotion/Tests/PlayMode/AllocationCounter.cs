using UnityEngine.Profiling;

namespace MeridianMotion.Tests
{
    /// <summary>
    /// Counts the managed heap allocations made on the main thread between Start and Stop, from the profiler's GC.Alloc
    /// samples, as the test framework's Is.Not.AllocatingGCMemory does. (GC.GetAllocatedBytesForCurrentThread, the
    /// obvious way, always gives 0 under Unity's Mono.) A count, not bytes: each allocation, of any size, is one.
    /// </summary>
    public static class AllocationCounter
    {
        private static Recorder recorder;

        /// <summary>Starts counting.</summary>
        public static void Start()
        {
            recorder ??= Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
        }

        /// <summary>Stops counting, and gives how many allocations were made since Start.</summary>
        public static int Stop()
        {
            recorder.enabled = false;
            recorder.CollectFromAllThreads();
            return recorder.sampleBlockCount;
        }
    }
}
