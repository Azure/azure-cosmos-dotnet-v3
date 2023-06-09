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
        public CounterOptions QuerySuccessMeter { get; }

        public CounterOptions QueryFailureMeter { get; }

        public CounterOptions ReadSuccessMeter { get; }

        public CounterOptions ReadFailureMeter { get; }

        public CounterOptions WriteSuccessMeter { get; }

        public CounterOptions WriteFailureMeter { get; }

        public TimerOptions QueryLatencyTimer { get; }

        public TimerOptions ReadLatencyTimer { get; }

        public TimerOptions InsertLatencyTimer { get; }

        public MetricsContext(BenchmarkConfig benchmarkConfig)
        {
            this.QuerySuccessMeter = new CounterOptions { Name = "#Query Successful Operations", Context = benchmarkConfig.LoggingContextIdentifier };
            this.QueryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations", Context = benchmarkConfig.LoggingContextIdentifier };

            this.ReadSuccessMeter = new CounterOptions { Name = "#Read Successful Operations", Context = benchmarkConfig.LoggingContextIdentifier };
            this.ReadFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations", Context = benchmarkConfig.LoggingContextIdentifier };

            this.WriteSuccessMeter = new CounterOptions { Name = "#Insert Successful Operations", Context = benchmarkConfig.LoggingContextIdentifier };
            this.WriteFailureMeter = new CounterOptions { Name = "#Insert Unsuccessful Operations", Context = benchmarkConfig.LoggingContextIdentifier };

            this.QueryLatencyTimer = new()
            {
                Name = "Query latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = benchmarkConfig.LoggingContextIdentifier,
                Reservoir = () => ReservoirProvider.GetReservoir(benchmarkConfig)
            };

            this.ReadLatencyTimer = new()
            {
                Name = "Read latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = benchmarkConfig.LoggingContextIdentifier,
                Reservoir = () => ReservoirProvider.GetReservoir(benchmarkConfig)
            };

            this.InsertLatencyTimer = new()
            {
                Name = "Insert latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = benchmarkConfig.LoggingContextIdentifier,
                Reservoir = () => ReservoirProvider.GetReservoir(benchmarkConfig)
            };
        }
    }
}