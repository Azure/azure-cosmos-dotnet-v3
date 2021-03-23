using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using HdrHistogram.Utilities;

namespace HdrHistogram.Benchmarking.LeadingZeroCount
{
    /// <summary>
    /// This is the base class for Leading Zero Count benchmarks.
    /// </summary>
    /// <remarks>
    /// This class is used so that we can test both 32bit and 64bit values. 
    /// As most values that will be recorded will likely be for values in the 32 bit range,
    /// and recording in this space will be most sensitive to latency, we want to be able to 
    /// benchmark each range of values.
    /// 
    /// The LeadingZeroCount algorithm is one of the hot spots in the HdrHistogram code base.
    /// This is why we are micro-benchmarking it.
    /// 
    /// Leading Zero Count (lzc or lzcnt) is also known as Count Leading Zeros (clz) and has numerous similar or closely related algorithms :
    ///  * Most Significant Bit (MSB), 
    ///  * Find First Set (FFS), 
    ///  * BitScan Forward,
    ///  * Hamming Weight
    ///  * DeBruijn
    /// </remarks>
    public abstract class LeadingZeroCountBenchmarkBase
    {
        private readonly int _maxBit;
        private readonly long[] _testValues;

        protected LeadingZeroCountBenchmarkBase(int maxBit)
        {
            _maxBit = maxBit;
            

            //Create array of +ve numbers in the 'maxBit' bit range (i.e. 32 bit or 64bit)
            var expectedData = GenerateTestData(maxBit);
            _testValues = expectedData.Select(d => d.Value).ToArray();
        }

        [BenchmarkDotNet.Attributes.GlobalSetup]
        public void OneOffValidationOfImplementations()
        {
            var expectedData = GenerateTestData(_maxBit);
            var functions = new Dictionary<string, Func<long, int>>
            {
                {"CurrentImpl", Bitwise.NumberOfLeadingZeros},
                {"IfAndShift", LeadingZeroCount.IfAndShift.GetLeadingZeroCount},
                //{"MathLog", LeadingZeroCount.MathLog.GetLeadingZeroCount},
                {"StringManipulation", LeadingZeroCount.StringManipulation.GetLeadingZeroCount},
                {"DeBruijn64Bits", LeadingZeroCount.DeBruijn64Bits.GetLeadingZeroCount},
                {"DeBruijn64BitsBitScanner", LeadingZeroCount.DeBruijn64BitsBitScanner.GetLeadingZeroCount},
                {"DeBruijnMultiplication", LeadingZeroCount.DeBruijnMultiplication.GetLeadingZeroCount},
                {"DeBruijn128Bits", LeadingZeroCount.DeBruijn128Bits.GetLeadingZeroCount},
                {"BBarry32BitIfShiftLookupWith64BitShiftBranch", LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch.GetLeadingZeroCount},
                {"BBarry32BitIfShiftLookupWith64BitShiftBranch_2", LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch_2.GetLeadingZeroCount},
                {"BBarry32BitIfShiftLookupWith64BitShiftBranch_3", LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch_3.GetLeadingZeroCount},
                {"BBarryIfShiftLookup", LeadingZeroCount.BBarryIfShiftLookup.GetLeadingZeroCount},
            };
            ValidateImplementations(expectedData, functions);
        }
        
        private static CalculationExpectation[] GenerateTestData(int maxBit)
        {
            //Create array of +ve numbers in the 'maxBit' bit range (i.e. 32 bit or 64bit)
            var smallValues = new[]
            {
                new CalculationExpectation(1L, 63),
                new CalculationExpectation(2L, 62),
                new CalculationExpectation(3L, 62),
                new CalculationExpectation(4L, 61),
                new CalculationExpectation(5L, 61),
            };
            var largeValues = Enumerable.Range(3, maxBit - 3)
                .Select(exp => new { Value = 1L << exp, LZC = 63 - exp })
                .SelectMany(x => new[]
                {
                    new CalculationExpectation(x.Value-1, x.LZC+1),
                    new CalculationExpectation(x.Value, x.LZC),
                    new CalculationExpectation(x.Value+1, x.LZC),
                })
                .Where(x => x.Value > 0)
                .Distinct()
                .ToArray();

            return smallValues.Concat(largeValues).ToArray();
        }

        private static void ValidateImplementations(IEnumerable<CalculationExpectation> expectedData, Dictionary<string, Func<long, int>> functions)
        {
            foreach (var data in expectedData)
            {
                var expected = data.Expected;
                foreach (var kvp in functions)
                {
                    var actual = kvp.Value(data.Value);
                    if (actual != expected)
                        throw new InvalidOperationException($"{kvp.Key} implementation invalid for {data.Value}. Expected {expected}, but was {actual}.");
                }
            }
        }
        
        

        [Benchmark(Baseline = true)]
        public int CurrentImplementation()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += Bitwise.NumberOfLeadingZeros(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int IfAndShift()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.IfAndShift.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int DeBruijnMultiplication()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.DeBruijnMultiplication.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int Debruijn64Bit()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.DeBruijn64Bits.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int DeBruijn64BitsBitScanner()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.DeBruijn64BitsBitScanner.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int Debruijn128Bit()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.DeBruijn128Bits.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }

        [Benchmark]
        public int StringManipulation()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.StringManipulation.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }
        [Benchmark]
        public int BBarry_imp1()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }
        [Benchmark]
        public int BBarry_imp2()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.BBarryIfShiftLookup.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }
        [Benchmark]
        public int BBarry_imp3()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch_2.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }
        [Benchmark]
        public int BBarry_imp4()
        {
            var sum = 0;
            for (int i = 0; i < _testValues.Length; i++)
            {
                sum += LeadingZeroCount.BBarry32BitIfShiftLookupWith64BitShiftBranch_3.GetLeadingZeroCount(_testValues[i]);
            }
            return sum;
        }


        private class CalculationExpectation
        {
            public long Value { get; }
            public int Expected { get; }

            public CalculationExpectation(long value, int expected)
            {
                Value = value;
                Expected = expected;
            }
        }
    }
}