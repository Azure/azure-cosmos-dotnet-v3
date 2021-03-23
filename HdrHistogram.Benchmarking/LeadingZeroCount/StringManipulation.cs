using System;

namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    public static class StringManipulation
    {
        public static int GetLeadingZeroCount(long value)
        {
            var str = Convert.ToString(value, 2).PadLeft(64, '0');
            return str.IndexOf('1');
        }
    }
}