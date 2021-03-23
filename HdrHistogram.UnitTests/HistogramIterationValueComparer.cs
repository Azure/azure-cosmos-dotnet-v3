using System.Collections;
using System.Collections.Generic;
using HdrHistogram.Iteration;

namespace HdrHistogram.UnitTests
{
    public sealed class HistogramIterationValueComparer : IComparer<HistogramIterationValue>, IComparer
    {
        public static readonly HistogramIterationValueComparer Instance = new HistogramIterationValueComparer();

        public int Compare(HistogramIterationValue x, HistogramIterationValue other)
        {
            var result = x.ValueIteratedTo.CompareTo(other.ValueIteratedTo);
            if (result != 0)
                return result;
            result = x.ValueIteratedFrom.CompareTo(other.ValueIteratedFrom);
            if (result != 0)
                return result;
            result = x.CountAtValueIteratedTo.CompareTo(other.CountAtValueIteratedTo);
            if (result != 0)
                return result;
            result = x.CountAddedInThisIterationStep.CompareTo(other.CountAddedInThisIterationStep);
            if (result != 0)
                return result;
            result = x.TotalCountToThisValue.CompareTo(other.TotalCountToThisValue);
            if (result != 0)
                return result;
            result = x.TotalValueToThisValue.CompareTo(other.TotalValueToThisValue);
            if (result != 0)
                return result;
            result = x.Percentile.CompareTo(other.Percentile);
            if (result != 0)
                return result;
            return x.PercentileLevelIteratedTo.CompareTo(other.PercentileLevelIteratedTo);
        }

        public int Compare(object x, object y)
        {
            return Compare((HistogramIterationValue)x, (HistogramIterationValue)y);
        }
    }
}