// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    internal sealed class TestInjections
    {
        public enum PipelineType
        {
            Passthrough,
            Specialized,
            OptimisticDirectExecution,
        }

        public TestInjections(bool simulate429s, bool simulateEmptyPages, bool enableOptimisticDirectExecution = false, ResponseStats responseStats = null)
        {
            this.SimulateThrottles = simulate429s;
            this.SimulateEmptyPages = simulateEmptyPages;
            this.Stats = responseStats;
            this.EnableOptimisticDirectExecution = enableOptimisticDirectExecution;
        }

        public bool EnableOptimisticDirectExecution { get; }

        public bool SimulateThrottles { get; }

        public bool SimulateEmptyPages { get; }

        public ResponseStats Stats { get; }

        public sealed class ResponseStats
        {
            public PipelineType? PipelineType { get; set; }
        }
    }
}
