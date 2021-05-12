using System;

namespace HdrHistogram.UnitTests
{
    public static class MathEx
    {
        public static double Round(this double value, int digits)
        {
            if (digits >= 0)
            {
                return Math.Round(value, digits);
            }
            else
            {
                digits = Math.Abs(digits);
                var temp = value / Math.Pow(10, digits);
                temp = Math.Round(temp, 0);
                temp = temp * Math.Pow(10, digits);
                return temp;
            }
        }
    }
}