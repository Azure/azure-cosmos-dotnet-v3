using System.Collections.Generic;

namespace HdrHistogram.UnitTests.Persistence
{
    public static class TestCaseGenerator
    {
        public static IEnumerable<long> PowersOfTwo(int maxBits)
        {
            for (int i = 0; i < maxBits; i++)
            {
                yield return (1L << i);
            }
        }
    }
}