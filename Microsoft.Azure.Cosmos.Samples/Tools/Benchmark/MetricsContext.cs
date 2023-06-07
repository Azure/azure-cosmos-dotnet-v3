//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;

    public class MetricsContext
    {
        public string ContextId { get; }

        public CounterOptions ReadSuccessMeter { get; }

        public CounterOptions ReadFailureMeter { get; }

        public CounterOptions WriteSuccessMeter { get; }

        public CounterOptions WriteFailureMeter { get; }

        public CounterOptions QuerySuccessMeter { get; }

        public CounterOptions QueryFailureMeter { get; }

        public TimerOptions LatencyTimer { get; }

        public MetricsContext(string contextId, BenchmarkConfig benchmarkConfig)
        {
            ContextId = contextId;

            ReadSuccessMeter = new CounterOptions { Name = "#Read Successful Operations", Context = ContextId };
            ReadFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations", Context = ContextId };
            WriteSuccessMeter = new CounterOptions { Name = "#Write Successful Operations", Context = ContextId };
            WriteFailureMeter = new CounterOptions { Name = "#Write Unsuccessful Operations", Context = ContextId };
            QuerySuccessMeter = new CounterOptions { Name = "#Query Successful Operations", Context = ContextId };
            QueryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations", Context = ContextId };

            LatencyTimer = new()
            {
                Name = "Latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = ContextId,
                Reservoir = () => ReservoirProvider.GetReservoir(benchmarkConfig)
            };
        }
    }
}