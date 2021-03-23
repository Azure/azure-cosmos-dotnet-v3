using System;

namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    /// <summary>
    /// Contributed from @BBarry at https://github.com/HdrHistogram/HdrHistogram.NET/issues/42
    /// This variation inlines all the shifts.
    /// It performs on par on LegacyJIT/CLR but significantly slower on RyuJIT.
    /// I assume it is because 7 branches vs the 4 above.
    /// </summary>
    internal static class BBarryIfShiftLookup
    {
        private static readonly int[] Lookup;

        static BBarryIfShiftLookup()
        {
            Lookup = new int[256];
            for (int i = 1; i < 256; ++i)
            {
                Lookup[i] = (int)(Math.Log(i) / Math.Log(2));
            }
        }

        public static int GetLeadingZeroCount(long value)
        {
            if (value >= 0x100000000000000) { return 7 - Lookup[value >> 56]; }
            if (value >= 0x1000000000000) { return 15 - Lookup[value >> 48]; }
            if (value >= 0x10000000000) { return 23 - Lookup[value >> 40]; }
            if (value >= 0x100000000) { return 31 - Lookup[value >> 32]; }
            if (value >= 0x1000000) { return 39 - Lookup[value >> 24]; }
            if (value >= 0x10000) { return 47 - Lookup[value >> 16]; }
            if (value >= 0x100) { return 55 - Lookup[value >> 8]; }
            return 63 - Lookup[value];
        }
    }
}