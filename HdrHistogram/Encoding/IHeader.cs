namespace HdrHistogram.Encoding
{
    /// <summary>
    /// Defines the header properties to be encoded for an HdrHistogram.
    /// </summary>
    public interface IHeader
    {
        /// <summary>
        /// The cookie value for the histogram.
        /// </summary>
        int Cookie { get; }
        /// <summary>
        /// The length in bytes of the payload body.
        /// </summary>
        int PayloadLengthInBytes { get; }
        /// <summary>
        /// The normalizing index offset.
        /// </summary>
        int NormalizingIndexOffset { get; } //Not currently implemented/used.
        /// <summary>
        /// THe number of significant digits that values are measured to.
        /// </summary>
        int NumberOfSignificantValueDigits { get; }
        /// <summary>
        /// The lowest trackable value for the histogram
        /// </summary>
        long LowestTrackableUnitValue { get; }
        /// <summary>
        /// The highest trackable value for the histogram
        /// </summary>
        long HighestTrackableValue { get; }
        /// <summary>
        /// Integer to double conversion ratio.
        /// </summary>
        double IntegerToDoubleValueConversionRatio { get; } //Not currently implemented/used.
        /// <summary>
        /// The amount of excess capacity that will not be needed.
        /// </summary>
        int CapacityEstimateExcess { get; }
    }
}