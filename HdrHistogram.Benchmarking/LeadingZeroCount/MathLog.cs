using System;

namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    public static class MathLog
    {
        //Stops being accurate at 281474976710655 (expected return value of 16 but returns 15).
        public static int GetLeadingZeroCount(long value)
        {
            if (value == 0) return 64;
            if (value == 1) return 63;

            return 63 - (int)(Math.Log(value) / Math.Log(2));
        }
    }
}