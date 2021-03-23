using HdrHistogram.Iteration;

namespace HdrHistogram.Output
{
    internal interface IOutputFormatter
    {
        void WriteHeader();
        void WriteValue(HistogramIterationValue value);
        void WriteFooter(HistogramBase histogram);
    }
}