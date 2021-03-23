namespace HdrHistogram
{
    /// <summary>
    /// The method definition for a histogram factory.
    /// </summary>
    /// <param name="instanceId">The instance id the histogram should be created with.</param>
    /// <param name="lowestDiscernibleValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
    /// Must be a positive integer that is &gt;= 1.
    /// May be internally rounded down to nearest power of 2.
    /// </param>
    /// <param name="highestTrackableValue">The highest value to be tracked by the histogram.
    /// Must be a positive integer that is &gt;= (2 * lowestTrackableValue).
    /// </param>
    /// <param name="numberOfSignificantValueDigits">
    /// The number of significant decimal digits to which the histogram will maintain value resolution and separation. 
    /// Must be a non-negative integer between 0 and 5.
    /// </param>
    public delegate HistogramBase HistogramFactoryDelegate(
        long instanceId, long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits);
}