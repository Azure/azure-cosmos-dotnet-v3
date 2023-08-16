namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Tracing;
    using OpenTelemetry.Metrics;

    internal class CosmosBenchmarkEventListener : EventListener
    {
        static readonly string CosmosBenchmarkEventSourceName = "Azure.Cosmos.Benchmark";
        
        private readonly MeterProvider meterProvider;
        private readonly MetricsCollector[] metricsCollectors;
        private readonly MetricCollectionWindow metricCollectionWindow;

        public CosmosBenchmarkEventListener(MeterProvider meterProvider, BenchmarkConfig config)
        {
            this.meterProvider = meterProvider;
            this.metricCollectionWindow ??= new MetricCollectionWindow(config.MetricsReportingIntervalInSec);

            this.metricsCollectors = new MetricsCollector[Enum.GetValues<BenchmarkOperationType>().Length];
            foreach (BenchmarkOperationType entry in Enum.GetValues<BenchmarkOperationType>())
            {
                this.metricsCollectors[(int)entry] = new MetricsCollector(entry);
            }
        }

        /// <summary>
        /// Override this method to get a list of all the eventSources that exist.  
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Because we want to turn on every EventSource, we subscribe to a callback that triggers
            // when new EventSources are created.  It is also fired when the EventListener is created
            // for all pre-existing EventSources.  Thus this callback get called once for every 
            // EventSource regardless of the order of EventSource and EventListener creation.  

            // For any EventSource we learn about, turn it on.   
            if (eventSource.Name == CosmosBenchmarkEventSourceName)
            {
                this.EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
            }
        }

        /// <summary>
        /// We override this method to get a callback on every event we subscribed to with EnableEvents
        /// </summary>
        /// <param name="eventData"></param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 2      // Successful
                || eventData.EventId == 3)  // Failure
            {
                int operationTypeIndex = (int)eventData.Payload[0];
                long durationInMs = (long)eventData.Payload[1];

                switch (eventData.EventId)
                {
                    case 2:
                        this.metricsCollectors[operationTypeIndex].OnOperationSuccess(TimeSpan.FromMilliseconds(durationInMs));
                        break;
                    case 3:
                        this.metricsCollectors[operationTypeIndex].OnOperationFailure(TimeSpan.FromMilliseconds(durationInMs));
                        break;
                    default:
                        break;
                }

                // Reset metricCollectionWindow and flush.
                // TODO: Revisit for concurrency 
                if (!this.metricCollectionWindow.IsValid)
                {
                    this.meterProvider.ForceFlush();
                    this.metricCollectionWindow.Reset();
                }
            }
        }
    }
}
