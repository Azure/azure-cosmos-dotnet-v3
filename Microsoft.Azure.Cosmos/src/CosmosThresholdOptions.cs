//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// This class describes the thresholds when more details diagnostics events are emitted, if subscribed, for an operation due to high latency,
    /// high RU consumption or high payload sizes.
    /// </summary>
    public class CosmosThresholdOptions
    {
        /// <summary>
        /// Can be used to define custom latency thresholds. When the latency threshold is exceeded more detailed
        /// diagnostics will be emitted (including the request diagnostics). There is some overhead of emitting the
        /// more detailed diagnostics - so recommendation is to choose latency thresholds that reduce the noise level
        /// and only emit detailed diagnostics when there is really business impact seen.
        /// The default value for the point operation latency threshold is 3 seconds.
        /// all operations except (ReadItem, CreateItem, UpsertItem, ReplaceItem, PatchItem or DeleteItem)
        /// </summary>
        /// <value>3 seconds</value>
        public TimeSpan NonPointOperationLatencyThreshold { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Can be used to define custom latency thresholds. When the latency threshold is exceeded more detailed
        /// diagnostics will be emitted (including the request diagnostics). There is some overhead of emitting the
        /// more detailed diagnostics - so recommendation is to choose latency thresholds that reduce the noise level
        /// and only emit detailed diagnostics when there is really business impact seen.
        /// The default value for the point operation latency threshold is 1 second.
        /// Point Operations are: (ReadItem, CreateItem, UpsertItem, ReplaceItem, PatchItem or DeleteItem)
        /// </summary>
        /// <value>1 second</value>
        public TimeSpan PointOperationLatencyThreshold { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Can be used to define a custom RU (request charge) threshold. When the threshold is exceeded more detailed
        /// diagnostics will be emitted (including the request diagnostics). There is some overhead of emitting the
        /// more detailed diagnostics - so recommendation is to choose a request charge threshold that reduces the noise
        /// level and only emits detailed diagnostics when the request charge is significantly higher than expected.
        /// </summary>
        public double? RequestChargeThreshold { get; set; } = null;

        /// <summary>
        /// Can be used to define a payload size threshold. When the threshold is exceeded for either request or
        /// response payloads more detailed diagnostics will be emitted (including the request diagnostics).
        /// There is some overhead of emitting the more detailed diagnostics - so recommendation is to choose a
        /// payload size threshold that reduces the noise level and only emits detailed diagnostics when the payload size
        /// is significantly higher than expected.
        /// </summary>
        public int? PayloadSizeThresholdInBytes { get; set; } = null;
    }
}
