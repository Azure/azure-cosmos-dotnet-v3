/*
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 * and released to the public domain, as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 */

using System;
using System.Threading;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    /// <summary>
    /// Records integer values, and provides stable interval <see cref="HistogramBase"/> samples from live recorded data without interrupting or stalling active recording of values.
    /// Each interval histogram provided contains all value counts accumulated since the previous interval histogram was taken.
    /// </summary>
    /// <remarks>
    /// This pattern is commonly used in logging interval histogram information while recording is ongoing.
    /// Recording calls are wait-free on architectures that support atomic increment operations, and are lock-free on architectures that do not.
    /// </remarks>
    public class Recorder : IRecorder
    {
        private static long _instanceIdSequencer = 1;

        private readonly object _gate = new object();
        private readonly long _instanceId = Interlocked.Increment(ref _instanceIdSequencer);
        private readonly WriterReaderPhaser _recordingPhaser = new WriterReaderPhaser();
        private readonly HistogramFactoryDelegate _histogramFactory;

        private HistogramBase _activeHistogram;
        private HistogramBase _inactiveHistogram;

        /// <summary>
        /// Creates a recorder that will delegate recording to histograms created from these parameters.
        /// </summary>
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
        /// <param name="histogramFactory">The factory to be used to actually create instances of <seealso cref="HistogramBase"/>.</param>
        public Recorder(
            long lowestDiscernibleValue,
            long highestTrackableValue,
            int numberOfSignificantValueDigits,
            HistogramFactoryDelegate histogramFactory)
        {
            _histogramFactory = histogramFactory;
            _activeHistogram = histogramFactory(_instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            _inactiveHistogram = histogramFactory(_instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            _activeHistogram.StartTimeStamp = DateTime.Now.MillisecondsSinceUnixEpoch();
        }

        /// <summary>
        /// Records a value in the histogram
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void RecordValue(long value)
        {
            var criticalValueAtEnter = _recordingPhaser.WriterCriticalSectionEnter();
            try
            {
                _activeHistogram.RecordValue(value);
            }
            finally
            {
                _recordingPhaser.WriterCriticalSectionExit(criticalValueAtEnter);
            }
        }

        /// <summary>
        /// Record a value in the histogram (adding to the value's current count)
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <param name="count">The number of occurrences of this value to record</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void RecordValueWithCount(long value, long count)
        {
            var criticalValueAtEnter = _recordingPhaser.WriterCriticalSectionEnter();
            try
            {
                _activeHistogram.RecordValueWithCount(value, count);
            }
            finally
            {
                _recordingPhaser.WriterCriticalSectionExit(criticalValueAtEnter);
            }
        }

        /// <summary>
        /// Record a value in the histogram.
        /// </summary>
        /// <param name="value">The value to record</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <paramref name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if <paramref name="value"/> is larger than <paramref name="expectedIntervalBetweenValueSamples"/></param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, 
        /// Histogram will auto-generate an additional series of decreasingly-smaller (down to the expectedIntervalBetweenValueSamples) value records.
        /// <para>
        /// Note: This is a at-recording correction method, as opposed to the post-recording correction method provided by currently unimplemented <c>CopyCorrectedForCoordinatedOmission</c> method.
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// </para>
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        public void RecordValueWithExpectedInterval(long value, long expectedIntervalBetweenValueSamples)
        {
            var criticalValueAtEnter = _recordingPhaser.WriterCriticalSectionEnter();
            try
            {
                _activeHistogram.RecordValueWithExpectedInterval(value, expectedIntervalBetweenValueSamples);
            }
            finally
            {
                _recordingPhaser.WriterCriticalSectionExit(criticalValueAtEnter);
            }
        }

        /// <summary>
        /// Get a new instance of an interval histogram, which will include a stable, consistent view of all value counts accumulated since the last interval histogram was taken.
        /// Calling <see cref="GetIntervalHistogram()"/> will reset the value counts, and start accumulating value counts for the next interval.
        /// </summary>
        /// <returns>A histogram containing the value counts accumulated since the last interval histogram was taken.</returns>
        public HistogramBase GetIntervalHistogram()
        {
            return GetIntervalHistogram(null);
        }

        /// <summary>
        /// Get a new instance of an interval histogram, which will include a stable, consistent view of all value counts accumulated since the last interval histogram was taken.
        /// Calling <see cref="GetIntervalHistogram()"/> will reset the value counts, and start accumulating value counts for the next interval.
        /// </summary>
        /// <param name="histogramToRecycle">a previously returned interval histogram that may be recycled to avoid allocation and copy operations.</param>
        /// <returns>A histogram containing the value counts accumulated since the last interval histogram was taken.</returns>
        /// <remarks>
        /// <see cref="GetIntervalHistogram(HistogramBase)"/> accepts a previously returned interval histogram that can be recycled internally to avoid allocation and content copying operations.
        /// It is therefore significantly more efficient for repeated use than <see cref="GetIntervalHistogram()"/> and <see cref="GetIntervalHistogramInto(HistogramBase)"/>.
        /// The provided <paramref name="histogramToRecycle"/> must be either be null or an interval histogram returned by a previous call to <see cref="GetIntervalHistogram(HistogramBase)"/> or <see cref="GetIntervalHistogram()"/>.
        /// NOTE: The caller is responsible for not recycling the same returned interval histogram more than once. 
        /// If the same interval histogram instance is recycled more than once, behavior is undefined.
        /// </remarks>
        public HistogramBase GetIntervalHistogram(HistogramBase histogramToRecycle)
        {
            lock (_gate)
            {
                // Verify that replacement histogram can validly be used as an inactive histogram replacement:
                ValidateFitAsReplacementHistogram(histogramToRecycle);
                _inactiveHistogram = histogramToRecycle;
                PerformIntervalSample();
                var sampledHistogram = _inactiveHistogram;
                _inactiveHistogram = null; // Once we expose the sample, we can't reuse it internally until it is recycled
                return sampledHistogram;
            }
        }

        /// <summary>
        /// Place a copy of the value counts accumulated since accumulated (since the last interval histogram was taken) into <paramref name="targetHistogram"/>.
        /// This will overwrite the existing data in <paramref name="targetHistogram"/>.
        /// Calling <see cref="GetIntervalHistogramInto(HistogramBase)"/> will reset the value counts, and start accumulating value counts for the next interval.
        /// </summary>
        /// <param name="targetHistogram">The histogram into which the interval histogram's data should be copied.</param>
        public void GetIntervalHistogramInto(HistogramBase targetHistogram)
        {
            lock (_gate)
            {
                PerformIntervalSample();
                _inactiveHistogram.CopyInto(targetHistogram);
            }
        }

        /// <summary>
        /// Reset any value counts accumulated thus far.
        /// </summary>
        public void Reset()
        {
            lock (_gate)
            {
                // the currently inactive histogram is reset each time we flip. So flipping twice resets both:
                PerformIntervalSample();
                PerformIntervalSample();
            }
        }

        private void PerformIntervalSample()
        {
            try
            {
                _recordingPhaser.ReaderLock();

                // Make sure we have an inactive version to flip in:
                if (_inactiveHistogram == null)
                {
                    _inactiveHistogram = _histogramFactory(_instanceId,
                        _activeHistogram.LowestTrackableValue,
                        _activeHistogram.HighestTrackableValue,
                        _activeHistogram.NumberOfSignificantValueDigits);
                }

                _inactiveHistogram.Reset();

                // Swap active and inactive histograms:
                var tempHistogram = _inactiveHistogram;
                _inactiveHistogram = _activeHistogram;
                _activeHistogram = tempHistogram;

                // Mark end time of previous interval and start time of new one:
                var now = DateTime.Now.MillisecondsSinceUnixEpoch();
                _activeHistogram.StartTimeStamp = now;
                _inactiveHistogram.EndTimeStamp = now;

                // Make sure we are not in the middle of recording a value on the previously active histogram:

                // Flip phase to make sure no recordings that were in flight pre-flip are still active:
                _recordingPhaser.FlipPhase(TimeSpan.FromMilliseconds(0.5));
            }
            finally
            {
                _recordingPhaser.ReaderUnlock();
            }
        }

        private void ValidateFitAsReplacementHistogram(HistogramBase replacementHistogram)
        {
            if(replacementHistogram !=null && replacementHistogram.InstanceId != _activeHistogram.InstanceId)
            {
                throw new InvalidOperationException(
                    $"Replacement histogram must have been obtained via a previous getIntervalHistogram() call from this {GetType().Name} instance");
            }
        }
    }
}