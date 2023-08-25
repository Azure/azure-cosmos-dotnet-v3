//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores client side QueryMetrics.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    sealed class ClientSideMetrics
    {
        public static readonly ClientSideMetrics Empty = new ClientSideMetrics(
            retries: 0,
            requestCharge: 0,
            fetchExecutionRanges: new List<FetchExecutionRange>());

        /// <summary>
        /// Initializes a new instance of the ClientSideMetrics class.
        /// </summary>
        /// <param name="retries">The number of retries required to execute the query.</param>
        /// <param name="requestCharge">The request charge incurred from executing the query.</param>
        /// <param name="fetchExecutionRanges">The fetch execution ranges from executing the query.</param>
        public ClientSideMetrics(
            long retries,
            double requestCharge,
            IEnumerable<FetchExecutionRange> fetchExecutionRanges)
        {
            this.Retries = retries;
            this.RequestCharge = requestCharge;
            this.FetchExecutionRanges = fetchExecutionRanges ?? throw new ArgumentNullException(nameof(fetchExecutionRanges));
        }

        /// <summary>
        /// Gets number of retries in the Azure DocumentDB database service (see IRetryPolicy.cs).
        /// </summary>
        public long Retries { get; }

        /// <summary>
        /// Gets the request charge for this continuation of the query.
        /// </summary>
        public double RequestCharge { get; }

        /// <summary>
        /// Gets the Fetch Execution Ranges for this continuation of the query.
        /// </summary>
        public IEnumerable<FetchExecutionRange> FetchExecutionRanges { get; }

        public ref struct Accumulator
        {
            public Accumulator(long retries, double requestCharge, IEnumerable<FetchExecutionRange> fetchExecutionRanges)
            {
                this.Retries = retries;
                this.RequestCharge = requestCharge;
                this.FetchExecutionRanges = fetchExecutionRanges;
            }

            public long Retries { get; }

            public double RequestCharge { get; }

            public IEnumerable<FetchExecutionRange> FetchExecutionRanges { get; }

            public Accumulator Accumulate(ClientSideMetrics clientSideMetrics)
            {
                if (clientSideMetrics == null)
                {
                    throw new ArgumentNullException(nameof(clientSideMetrics));
                }

                return new Accumulator(
                    retries: this.Retries + clientSideMetrics.Retries,
                    requestCharge: this.RequestCharge + clientSideMetrics.RequestCharge,
                    fetchExecutionRanges: (this.FetchExecutionRanges ?? Enumerable.Empty<FetchExecutionRange>()).Concat(clientSideMetrics.FetchExecutionRanges));
            }

            public static ClientSideMetrics ToClientSideMetrics(Accumulator accumulator)
            {
                return new ClientSideMetrics(
                    retries: accumulator.Retries,
                    requestCharge: accumulator.RequestCharge,
                    fetchExecutionRanges: accumulator.FetchExecutionRanges);
            }
        }
    }
}
