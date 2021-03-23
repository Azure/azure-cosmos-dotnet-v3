using System;

namespace HdrHistogram
{
    /// <summary>
    /// Provides factory methods to define the features of your histogram.
    /// </summary>
    public abstract class HistogramFactory
    {
        /// <summary>
        /// Private constructor to force usage via the Static starter methods.
        /// </summary>
        private HistogramFactory()
        {
        }

        /// <summary>
        /// The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. 
        /// May be internally rounded down to nearest power of 2.
        /// </summary>
        protected long LowestTrackableValue { get; set; } = 1;

        /// <summary>
        /// The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * <see cref="LowestTrackableValue"/>).
        /// </summary>
        protected long HighestTrackableValue { get; set; } = TimeStamp.Minutes(10);

        /// <summary>
        /// The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </summary>
        protected int NumberOfSignificantValueDigits { get; set; } = 3;

        

        /// <summary>
        /// Specifies that the Histogram to be created should be thread safe when written to from multiple threads.
        /// </summary>
        /// <returns>Returns a <see cref="HistogramFactory"/> that is set to return a threadsafe writer.</returns>
        public abstract HistogramFactory WithThreadSafeWrites();

        /// <summary>
        /// Specifies that the consumer will need to be able to read Histogram values in a thread safe manner.
        /// This will mean <see cref="Recorder"/> will be used to wrap the Histogram, allowing thread safe reads.
        /// </summary>
        /// <returns>Returns a <see cref="RecorderFactory"/> which can create recorders. Recorders allow for threadsafe reads.</returns>
        public abstract RecorderFactory WithThreadSafeReads();

        /// <summary>
        /// A factory-method to create the Histogram.
        /// </summary>
        /// <param name="lowestDiscernibleValue">
        /// The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. 
        /// May be internally rounded down to nearest power of 2.
        /// </param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * <paramref name="lowestDiscernibleValue"/>).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        /// <returns>Returns a newly created <see cref="HistogramBase"/> instance defined by the settings of the current instance of <see cref="HistogramFactory"/>.</returns>
        public abstract HistogramBase Create(
            long lowestDiscernibleValue,
            long highestTrackableValue,
            int numberOfSignificantValueDigits);

        /// <summary>
        /// A factory-method to create the Histogram.
        /// </summary>
        /// <param name="instanceId">An identifier for this instance.</param>
        /// <param name="lowestDiscernibleValue">
        /// The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. 
        /// May be internally rounded down to nearest power of 2.
        /// </param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * <paramref name="lowestDiscernibleValue"/>).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        /// <returns>Returns a newly created <see cref="HistogramBase"/> instance defined by the settings of the current instance of <see cref="HistogramFactory"/>.</returns>
        public abstract HistogramBase Create(
            long instanceId,
            long lowestDiscernibleValue,
            long highestTrackableValue,
            int numberOfSignificantValueDigits);

        /// <summary>
        /// Specifies the lowest value the Histogram should be configured to record.
        /// </summary>
        /// <param name="lowestDiscernibleValue">
        /// The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. 
        /// May be internally rounded down to nearest power of 2.
        /// </param>
        /// <returns>The <see cref="HistogramFactory"/> configured with the specified minimum allowed value.</returns>
        public HistogramFactory WithValuesFrom(long lowestDiscernibleValue)
        {
            LowestTrackableValue = lowestDiscernibleValue;
            return this;
        }

        /// <summary>
        /// Specifies the highest value the Histogram should be configured to record.
        /// </summary>
        /// <param name="highestTrackableValue">
        /// The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * <see cref="LowestTrackableValue"/>).
        /// </param>
        /// <returns>The <see cref="HistogramFactory"/> configured with the specified maximum allowed value.</returns>
        public HistogramFactory WithValuesUpTo(long highestTrackableValue)
        {
            HighestTrackableValue = highestTrackableValue;
            return this;
        }

        /// <summary>
        /// Specifies the number of significant figures that the Histogram should record.
        /// </summary>
        /// <param name="numberOfSignificantValueDigits">
        /// The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        /// <returns>The <see cref="HistogramFactory"/> configured with the specified maximum allowed value.</returns>
        public HistogramFactory WithPrecisionOf(int numberOfSignificantValueDigits)
        {
            NumberOfSignificantValueDigits = numberOfSignificantValueDigits;
            return this;
        }
        
        /// <summary>
        /// Creates the histogram as configured by this factory instance.
        /// </summary>
        /// <returns>A newly created instance of <see cref="HistogramBase"/>.</returns>
        public HistogramBase Create()
        {
            return Create(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
        }




        /// <summary>
        /// Specify that the Histogram should be able to record count values in the 64bit range.
        /// </summary>
        /// <returns>The <see cref="HistogramFactory"/> configured for 64bit bucket sizes.</returns>
        public static HistogramFactory With64BitBucketSize()
        {
            return new LongHistogramFactory();
        }

        /// <summary>
        /// Specify that the Histogram should be able to record count values in the 32bit range.
        /// </summary>
        /// <returns>The <see cref="HistogramFactory"/> configured for 64bit bucket sizes.</returns>
        public static HistogramFactory With32BitBucketSize()
        {
            return new IntHistogramFactory();
        }

        /// <summary>
        /// Specify that the Histogram should be able to record count values in the 32bit range.
        /// </summary>
        /// <returns>The <see cref="HistogramFactory"/> configured for 64bit bucket sizes.</returns>
        public static HistogramFactory With16BitBucketSize()
        {
            return new ShortHistogramFactory();
        }



        private sealed class LongHistogramFactory : HistogramFactory
        {
            public override HistogramBase Create(long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            {
                return new LongHistogram(lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramBase Create(long instanceId, long lowestDiscernibleValue, long highestTrackableValue,
                int numberOfSignificantValueDigits)
            {
                return new LongHistogram(instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramFactory WithThreadSafeWrites()
            {
                return new LongConcurrentHistogramFactory(this);
            }

            public override RecorderFactory WithThreadSafeReads()
            {
                return new RecorderFactory(this);
            }
        }

        private sealed class IntHistogramFactory : HistogramFactory
        {
            public override HistogramBase Create(long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            {
                return new IntHistogram(lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramBase Create(long instanceId, long lowestDiscernibleValue, long highestTrackableValue,
                int numberOfSignificantValueDigits)
            {
                return new IntHistogram(instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramFactory WithThreadSafeWrites()
            {
                return new IntConcurrentHistogramFactory(this);
            }

            public override RecorderFactory WithThreadSafeReads()
            {
                return new RecorderFactory(this);
            }
        }

        private sealed class ShortHistogramFactory : HistogramFactory
        {
            public override HistogramBase Create(long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            {
                return new ShortHistogram(lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramBase Create(long instanceId, long lowestDiscernibleValue, long highestTrackableValue,
                int numberOfSignificantValueDigits)
            {
                return new ShortHistogram(instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramFactory WithThreadSafeWrites()
            {
                throw new NotSupportedException("Short(16bit) Histograms do not support thread safe writes.");
            }

            public override RecorderFactory WithThreadSafeReads()
            {
                return new RecorderFactory(this);
            }
        }

        private sealed class LongConcurrentHistogramFactory : HistogramFactory
        {
            public LongConcurrentHistogramFactory(HistogramFactory histogramFactory)
            {
                LowestTrackableValue = histogramFactory.LowestTrackableValue;
                HighestTrackableValue = histogramFactory.HighestTrackableValue;
                NumberOfSignificantValueDigits = histogramFactory.NumberOfSignificantValueDigits;
            }

            public override HistogramFactory WithThreadSafeWrites()
            {
                return this;
            }

            public override RecorderFactory WithThreadSafeReads()
            {
                return new RecorderFactory(this);
            }

            public override HistogramBase Create(long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            {
                return new LongConcurrentHistogram(lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramBase Create(long instanceId, long lowestDiscernibleValue, long highestTrackableValue,
                int numberOfSignificantValueDigits)
            {
                return new LongConcurrentHistogram(instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }
        }

        private sealed class IntConcurrentHistogramFactory : HistogramFactory
        {
            public IntConcurrentHistogramFactory(HistogramFactory histogramFactory)
            {
                LowestTrackableValue = histogramFactory.LowestTrackableValue;
                HighestTrackableValue = histogramFactory.HighestTrackableValue;
                NumberOfSignificantValueDigits = histogramFactory.NumberOfSignificantValueDigits;
            }

            public override HistogramFactory WithThreadSafeWrites()
            {
                return this;
            }

            public override RecorderFactory WithThreadSafeReads()
            {
                return new RecorderFactory(this);
            }

            public override HistogramBase Create(long lowestDiscernibleValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            {
                return new IntConcurrentHistogram(lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }

            public override HistogramBase Create(long instanceId, long lowestDiscernibleValue, long highestTrackableValue,
                int numberOfSignificantValueDigits)
            {
                return new IntConcurrentHistogram(instanceId, lowestDiscernibleValue, highestTrackableValue, numberOfSignificantValueDigits);
            }
        }


        /// <summary>
        /// Factory for creating Recorders for thread safe reading of histograms.
        /// </summary>
        public sealed class RecorderFactory
        {
            private readonly HistogramFactory _histogramBuilder;

            internal RecorderFactory(HistogramFactory histogramBuilder)
            {
                _histogramBuilder = histogramBuilder;
            }

            /// <summary>
            /// Creates the recorder as configured by this factory instance.
            /// </summary>
            /// <returns>A newly created instance of <see cref="Recorder"/>.</returns>
            public Recorder Create()
            {
                return new Recorder(
                    _histogramBuilder.LowestTrackableValue, 
                    _histogramBuilder.HighestTrackableValue, 
                    _histogramBuilder.NumberOfSignificantValueDigits, 
                    _histogramBuilder.Create);
            }
        }
    }
}