//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    /// <summary>
    /// This class stores all the default values used during cross partition queries.
    /// </summary>
    internal sealed class ParallelQueryConfig
    {
        /// <summary>
        /// If the client specifies a MaxItemCount as -1, we return documents in batches of 100
        /// </summary>
        public readonly int ClientInternalPageSize;

        /// <summary>
        /// Default maximum number of documents cached at the client side, if it is not specified in the feedOptions.
        /// </summary>
        public readonly long DefaultMaximumBufferSize;

        /// <summary>
        /// We adaptively increase the number of threads as we see partitions are continuing to return results. 2 => we double the number of threads.
        /// </summary>
        public readonly int AutoModeTasksIncrementFactor;

        /// <summary>
        /// This is the value we overwrite with in the above case. -1 => the server returns dynamic number of results. Overwriting -1 by very high number doesn't have any significant impact in performance. -1, tries to return maximum number of documents possible per roundtrip
        /// </summary>
        public readonly int ClientInternalMaxItemCount;

        /// <summary>
        /// Making a backend call is equivalent to a Network Call. Here 1 indicates that if the client machine has 4 processor, we would allow at max (4*1) = 4 parallel calls to the backend. Of course, the number of parallel call won't exceed the number of partitions that needs to be visited for the query under consideration.
        /// </summary> 
        public readonly int NumberOfNetworkCallsPerProcessor;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        private static readonly ParallelQueryConfig DefaultInstance = new ParallelQueryConfig(
            clientInternalMaxItemCount: 100,
            defaultMaximumBufferSize: 100,
            clientInternalPageSize: 100,
            autoModeTasksIncrementFactor: 2,
            numberOfNetworkCallsPerProcessor: 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="ParallelQueryConfig"/> class.
        /// </summary>
        /// <param name="clientInternalMaxItemCount">The client's internal max item count.</param>
        /// <param name="defaultMaximumBufferSize">The default maximum buffer size.</param>
        /// <param name="clientInternalPageSize">The client's internal page size.</param>
        /// <param name="autoModeTasksIncrementFactor">The increment factor for auto mode.</param>
        /// <param name="numberOfNetworkCallsPerProcessor">Number of network calls per processor.</param>
        private ParallelQueryConfig(
            int clientInternalMaxItemCount,
            int defaultMaximumBufferSize,
            int clientInternalPageSize,
            int autoModeTasksIncrementFactor,
            int numberOfNetworkCallsPerProcessor)
        {
            this.ClientInternalMaxItemCount = clientInternalMaxItemCount;
            this.DefaultMaximumBufferSize = defaultMaximumBufferSize;
            this.ClientInternalPageSize = clientInternalPageSize;
            this.AutoModeTasksIncrementFactor = autoModeTasksIncrementFactor;
            this.NumberOfNetworkCallsPerProcessor = numberOfNetworkCallsPerProcessor;
        }

        /// <summary>
        /// Gets the configs for parallel queries.
        /// </summary>
        /// <returns>The configs for parallel queries.</returns>
        public static ParallelQueryConfig GetConfig()
        {
            return ParallelQueryConfig.DefaultInstance;
        }
    }
}