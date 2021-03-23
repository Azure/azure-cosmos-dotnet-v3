using System.Linq;
using BenchmarkDotNet.Attributes;

namespace HdrHistogram.Benchmarking.Recording
{
    public class Recording32BitBenchmark
    {
        private readonly long[] _testValues;
        private readonly LongHistogram _longHistogram;
        private readonly LongConcurrentHistogram _longConcurrentHistogram;
        private readonly IntHistogram _intHistogram;
        private readonly IntConcurrentHistogram _intConcurrentHistogram;
        private readonly ShortHistogram _shortHistogram;
        private readonly Recorder _longRecorder;
        private readonly Recorder _longConcurrentRecorder;
        private readonly Recorder _intRecorder;
        private readonly Recorder _intConcurrentRecorder;
        private readonly Recorder _shortRecorder;

        public Recording32BitBenchmark()
        {
            const int lowestTrackableValue = 1;
            var highestTrackableValue = TimeStamp.Minutes(10);
            const int numberOfSignificantValueDigits = 3;

            _testValues = TestValues(highestTrackableValue);
            
            _longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            _intHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            _shortHistogram = new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);

            _longConcurrentHistogram = new LongConcurrentHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            _intConcurrentHistogram = new IntConcurrentHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);

            _longRecorder = new Recorder(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, (id, low, hi, sf) => new LongHistogram(id, low, hi, sf));
            _longConcurrentRecorder = new Recorder(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, (id, low, hi, sf) => new LongConcurrentHistogram(id, low, hi, sf));
            _intRecorder = new Recorder(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, (id, low, hi, sf) => new IntHistogram(id, low, hi, sf));
            _intConcurrentRecorder = new Recorder(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, (id, low, hi, sf) => new IntConcurrentHistogram(id, low, hi, sf));
            _shortRecorder = new Recorder(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, (id, low, hi, sf) => new ShortHistogram(id, low, hi, sf));
        }

        private static long[] TestValues(long highestTrackableValue)
        {
            //Create array of +ve numbers in the 'maxBit' bit range (i.e. 32 bit or 64bit)
            //  32 bit values are the 89 values
            //  1,2,3,4,5,7,8,9,15,16,17,31,32,33,63,64,65,127,128,129,255,256,257,511,512,513,1023,1024,1025,2047,2048,2049,4095,4096,4097,8191,8192,8193,
            //  16383,16384,16385,32767,32768,32769,65535,65536,65537,131071,131072,131073,262143,262144,262145,524287,524288,524289,1048575,1048576,1048577,
            //  2097151,2097152,2097153,4194303,4194304,4194305,8388607,8388608,8388609,16777215,16777216,16777217,33554431,33554432,33554433,
            //  67108863,67108864,67108865,134217727,134217728,134217729,268435455,268435456,268435457,536870911,536870912,536870913,1073741823,1073741824,1073741825
            //These value are choosen as they are the edge case values of where our bucket boundaries lie. i.e.
            //  a power of 2
            //  1 less than a power of 2
            //  1 more than a power of 2
            return Enumerable.Range(0, 32)
                .Select(exp => new { Value = 1L << exp, LZC = 63 - exp })
                .SelectMany(x => new[]
                {
                    x.Value-1,
                    x.Value,
                    x.Value+1,
                })
                .Where(x => x > 0)
                .Where(x => x < highestTrackableValue)
                .Distinct()
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        public long LongHistogramRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _longHistogram.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long LongConcurrentHistogramRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _longConcurrentHistogram.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long IntHistogramRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _intHistogram.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long IntConcurrentHistogramRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _intConcurrentHistogram.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long ShortHistogramRecording()
        {
            for (int i = 0; i < _testValues.Length; i++)
            {
                _shortHistogram.RecordValue(_testValues[i]);
            }
            return _shortHistogram.TotalCount;
        }

        [Benchmark]
        public long LongRecorderRecording()
        {
            long counter = 0L;

            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _longRecorder.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long LongConcurrentRecorderRecording()
        {
            long counter = 0L;

            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _longConcurrentRecorder.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long IntRecorderRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _intRecorder.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long IntConcurrentRecorderRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _intConcurrentRecorder.RecordValue(value);
                counter += value;
            }
            return counter;
        }

        [Benchmark]
        public long ShortRecorderRecording()
        {
            long counter = 0L;
            for (int i = 0; i < _testValues.Length; i++)
            {
                var value = _testValues[i];
                _shortRecorder.RecordValue(value);
                counter += value;
            }
            return counter;
        }
    }
}
