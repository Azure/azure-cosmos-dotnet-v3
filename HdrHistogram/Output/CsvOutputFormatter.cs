using System.IO;
using HdrHistogram.Iteration;

namespace HdrHistogram.Output
{
    internal sealed class CsvOutputFormatter : IOutputFormatter
    {
        private readonly string _percentileFormatString;
        private readonly string _lastLinePercentileFormatString;
        private readonly TextWriter _textWriter;
        private readonly double _outputValueUnitScalingRatio;

        public CsvOutputFormatter(TextWriter textWriter, int significantDigits, double outputValueUnitScalingRatio)
        {
            _textWriter = textWriter;
            _outputValueUnitScalingRatio = outputValueUnitScalingRatio;
            _percentileFormatString = "{0:F" + significantDigits + "},{1:F12},{2},{3:F2}\n";
            _lastLinePercentileFormatString = "{0:F" + significantDigits + "},{1:F12},{2},Infinity\n";
        }

        public void WriteHeader()
        {
            _textWriter.Write("\"Value\",\"Percentile\",\"TotalCount\",\"1/(1-Percentile)\"\n");
        }

        public void WriteValue(HistogramIterationValue iterationValue)
        {
            var scaledValue = iterationValue.ValueIteratedTo / _outputValueUnitScalingRatio;
            var percentile = iterationValue.PercentileLevelIteratedTo / 100.0D;

            if (iterationValue.IsLastValue())
            {
                _textWriter.Write(_lastLinePercentileFormatString, scaledValue, percentile, iterationValue.TotalCountToThisValue);
            }
            else
            {
                _textWriter.Write(_percentileFormatString, scaledValue, percentile, iterationValue.TotalCountToThisValue, 1 / (1.0D - percentile));

            }
        }
        
        public void WriteFooter(HistogramBase histogram)
        {
            //No op
        }
    }
}