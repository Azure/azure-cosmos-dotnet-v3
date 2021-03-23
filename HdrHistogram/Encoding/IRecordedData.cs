namespace HdrHistogram.Encoding
{
    /// <summary>
    /// Defines the histogram data to be recorded
    /// </summary>
    public interface IRecordedData
    {
        /// <summary>
        /// The cookie value for the histogram.
        /// </summary>
        int Cookie { get; }
        /// <summary>
        /// The normalizing index offset.
        /// </summary>
        int NormalizingIndexOffset { get; } //Required? What is it?
        /// <summary>
        /// THe number of significant digits that values are measured to.
        /// </summary>
        int NumberOfSignificantValueDigits { get; }
        /// <summary>
        /// The lowest trackable value for the histogram
        /// </summary>
        long LowestDiscernibleValue { get; }        //TODO: Use either LowestDiscernibleValue or LowestTrackableUnitValue but not both. -LC
        /// <summary>
        /// The highest trackable value for the histogram
        /// </summary>
        long HighestTrackableValue { get; }
        /// <summary>
        /// Integer to double conversion ratio.
        /// </summary>
        double IntegerToDoubleValueConversionRatio { get; }
        /// <summary>
        /// The actual array of counts.
        /// </summary>
        long[] Counts { get; }
    }
}
