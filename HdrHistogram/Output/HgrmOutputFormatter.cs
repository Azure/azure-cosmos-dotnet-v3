using System.IO;
using HdrHistogram.Iteration;

namespace HdrHistogram.Output
{

    internal sealed class HgrmOutputFormatter : IOutputFormatter
    {
        private readonly TextWriter _printStream;
        private readonly double _outputValueUnitScalingRatio;
        private readonly string _percentileFormatString;
        private readonly string _lastLinePercentileFormatString;
        private readonly string _footerLine1FormatString;
        private readonly string _footerLine2FormatString;
        private readonly string _footerLine3FormatString;

        public HgrmOutputFormatter(TextWriter printStream, int significantDigits, double outputValueUnitScalingRatio)
        {
            _printStream = printStream;
            _outputValueUnitScalingRatio = outputValueUnitScalingRatio;
            _percentileFormatString =         "{0,12:F" + significantDigits + "} {1,2:F12} {2,10} {3,14:F2}\n";
            _lastLinePercentileFormatString = "{0,12:F" + significantDigits + "} {1,2:F12} {2,10}\n";
            _footerLine1FormatString = "#[Mean    = {0,12:F" + significantDigits + "}, StdDeviation   = {1,12:F" + significantDigits + "}]\n";
            _footerLine2FormatString = "#[Max     = {0,12:F" + significantDigits + "}, Total count    = {1,12}]\n";
            _footerLine3FormatString = "#[Buckets = {0,12}, SubBuckets     = {1,12}]\n";
        }

        public void WriteHeader()
        {
            _printStream.Write("{0,12} {1,14} {2,10} {3,14}\n\n", "Value", "Percentile", "TotalCount", "1/(1-Percentile)");
        }

        public void WriteValue(HistogramIterationValue iterationValue)
        {
            var scaledValue = iterationValue.ValueIteratedTo / _outputValueUnitScalingRatio;
            var percentile = iterationValue.PercentileLevelIteratedTo / 100.0D;

            if (iterationValue.IsLastValue())
            {
                _printStream.Write(_lastLinePercentileFormatString, scaledValue, percentile, iterationValue.TotalCountToThisValue);
            }
            else
            {
                _printStream.Write(_percentileFormatString, scaledValue, percentile, iterationValue.TotalCountToThisValue, 1 / (1.0D - percentile));
                
            }
        }
        
        public void WriteFooter(HistogramBase histogram)
        {
            // Calculate and output mean and std. deviation.
            // Note: mean/std. deviation numbers are very often completely irrelevant when
            // data is extremely non-normal in distribution (e.g. in cases of strong multi-modal
            // response time distribution associated with GC pauses). However, reporting these numbers
            // can be very useful for contrasting with the detailed percentile distribution
            // reported by outputPercentileDistribution(). It is not at all surprising to find
            // percentile distributions where results fall many tens or even hundreds of standard
            // deviations away from the mean - such results simply indicate that the data sampled
            // exhibits a very non-normal distribution, highlighting situations for which the std.
            // deviation metric is a useless indicator.

            var mean = histogram.GetMean() / _outputValueUnitScalingRatio;
            var stdDeviation = histogram.GetStdDeviation() / _outputValueUnitScalingRatio;
            _printStream.Write(_footerLine1FormatString, mean, stdDeviation);
            _printStream.Write(_footerLine2FormatString, histogram.GetMaxValue() / _outputValueUnitScalingRatio, histogram.TotalCount);
            _printStream.Write(_footerLine3FormatString, histogram.BucketCount, histogram.SubBucketCount);
        }
    }
}
