namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    //Only valid for 32bit values.
    public static class Magic32Bit
    {
        public static int GetLeadingZeroCount(long value)
        {
            var x = (int) value;
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (sizeof(long) * 8 - Ones(x));
        }
        private static int Ones(int x)
        {
            x -= ((x >> 1) & 0x55555555);
            x = (((x >> 2) & 0x33333333) + (x & 0x33333333));
            x = (((x >> 4) + x) & 0x0f0f0f0f);
            x += (x >> 8);
            x += (x >> 16);
            return (x & 0x0000003f);
        }
    }
}