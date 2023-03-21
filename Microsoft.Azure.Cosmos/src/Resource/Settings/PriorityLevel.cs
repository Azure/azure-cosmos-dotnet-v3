//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary> 
    /// Valid values of Priority Level for a request
    /// </summary>
    /// <remarks>
    /// Setting priority level only has an effect if Priority Based Execution is enabled.
    /// If it is not enabled, the priority level is ignored by the backend.
    /// Default PriorityLevel for each request is treated as High. It can be explicitly set to Low for some requests.
    /// When Priority based execution is enabled, if there are more requests than the configured RU/S in a second, 
    /// then Cosmos DB will throttle low priority requests to allow high priority requests to execute.
    /// This does not limit the throughput available to each priority level. Each priority level can consume the complete
    /// provisioned throughput in absence of the other. If both priorities are present and the user goes above the
    /// configured RU/s, low priority requests start getting throttled first to allow execution of mission critical workloads.
    /// </remarks>
    /// <seealso href="https://aka.ms/CosmosDB/PriorityBasedExecution"/>

#if PREVIEW
    public
#else
    internal
#endif
    enum PriorityLevel
    {
        /// <summary> 
        /// High Priority
        /// </summary>
        High = 1,

        /// <summary> 
        /// Low Priority
        /// </summary>
        Low = 2,
    }
}
