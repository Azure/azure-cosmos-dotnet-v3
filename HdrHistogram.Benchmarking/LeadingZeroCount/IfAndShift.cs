namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    //Can we optimise this impl for value between 1->long.max
    //  i.e. not 0, not -ve and not values over 2^63 (ulong)
    public static class IfAndShift
    {
        
        public static int GetLeadingZeroCount(long input)
        {
            return (int) CountLeadingZeros((ulong) input);
        }

        //http://stackoverflow.com/questions/31374628/fast-way-of-finding-most-and-least-significant-bit-set-in-a-64-bit-integer?lq=1
        public static ulong CountLeadingZeros(ulong input)
        {
            if (input == 0) return 64;

            ulong n = 1;

            if ((input >> 32) == 0) { n = n + 32; input = input << 32; }
            if ((input >> 48) == 0) { n = n + 16; input = input << 16; }
            if ((input >> 56) == 0) { n = n + 8; input = input << 8; }
            if ((input >> 60) == 0) { n = n + 4; input = input << 4; }
            if ((input >> 62) == 0) { n = n + 2; input = input << 2; }
            n = n - (input >> 63);

            return n;
        }
    }
}