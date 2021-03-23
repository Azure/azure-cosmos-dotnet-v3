using System;

namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    /// <summary>
    /// Contributed from @BBarry at https://github.com/HdrHistogram/HdrHistogram.NET/issues/42
    /// This variation perfoms very well. Similar profile to the "Current Impl".
    /// Faster on LegacyJIT/CLR, but much slower on RyuJIT (CLR & Core)
    /// </summary>
    public static class BBarry32BitIfShiftLookupWith64BitShiftBranch
    {
        private static readonly int[] Lookup;

        static BBarry32BitIfShiftLookupWith64BitShiftBranch()
        {
            Lookup = new int[256];
            for (int i = 1; i < 256; ++i)
            {
                Lookup[i] = (int)(Math.Log(i) / Math.Log(2));
            }
        }
        public static int GetLeadingZeroCount(long value)
        {
            //TODO: Test this with just < instead of <=? i.e. const of int.Max+1;
            if (value <= int.MaxValue)
                return 63 - Log2((int)value);
            if (value <= uint.MaxValue)
                return 62 - Log2((int)(value >> 1));
            return 31 - Log2((int)(value >> 32));
        }

        private static int Log2(int i)
        {
            if (i >= 0x1000000) { return Lookup[i >> 24] + 24; }
            if (i >= 0x10000) { return Lookup[i >> 16] + 16; }
            if (i >= 0x100) { return Lookup[i >> 8] + 8; }
            return Lookup[i];
        }
    }

    /// <summary>
    /// Contributed from @BBarry at https://github.com/HdrHistogram/HdrHistogram.NET/issues/42
    /// This variation perfoms very well. Similar profile to the "Current Impl".
    /// Faster on LegacyJIT/CLR, but much slower on RyuJIT (CLR & Core)
    /// </summary>
    public static class BBarry32BitIfShiftLookupWith64BitShiftBranch_2
    {
        private static readonly int[] Lookup;
        static BBarry32BitIfShiftLookupWith64BitShiftBranch_2()
        {
            Lookup = new int[256];
            for (int i = 1; i < 256; ++i)
            {
                Lookup[i] = (int)(Math.Log(i) / Math.Log(2));
            }
        }
        public static int GetLeadingZeroCount(long value)
        {
            if (value <= int.MaxValue)
                return 63 - Log2((uint)value);
            return 32 - Log2((uint)(value >> 31));
        }

        private static int Log2(uint i)
        {
            if (i >= 0x1000000) { return Lookup[i >> 24] + 24; }
            if (i >= 0x10000) { return Lookup[i >> 16] + 16; }
            if (i >= 0x100) { return Lookup[i >> 8] + 8; }
            return Lookup[i];
        }
    }

    /// <summary>
    /// Contributed from @BBarry at https://github.com/HdrHistogram/HdrHistogram.NET/issues/42
    /// This variation perfoms very well. Similar profile to the "Current Impl".
    /// Faster on LegacyJIT/CLR, but much slower on RyuJIT (CLR & Core)
    /// This just compares only lessThan operator instead of lessThanEqualTo. #completeness
    /// </summary>
    public static class BBarry32BitIfShiftLookupWith64BitShiftBranch_3
    {
        private static readonly int[] Lookup;
        private const long IntOverflow = int.MaxValue + 1L;
        static BBarry32BitIfShiftLookupWith64BitShiftBranch_3()
        {
            Lookup = new int[256];
            for (int i = 1; i < 256; ++i)
            {
                Lookup[i] = (int)(Math.Log(i) / Math.Log(2));
            }
        }
        public static int GetLeadingZeroCount(long value)
        {
            if (value < IntOverflow)
                return 63 - Log2((uint)value);
            return 32 - Log2((uint)(value >> 31));
        }

        private const int Bit24Range = 0x1000000 - 1;
        private const int Bit16Range = 0x10000 - 1;
        private const int Bit8Range = 0x100 - 1;
        private static int Log2(uint i)
        {
            if (i > Bit24Range) { return Lookup[i >> 24] + 24; }
            if (i > Bit16Range) { return Lookup[i >> 16] + 16; }
            if (i > Bit8Range) { return Lookup[i >> 8] + 8; }
            return Lookup[i];
        }
    }
}