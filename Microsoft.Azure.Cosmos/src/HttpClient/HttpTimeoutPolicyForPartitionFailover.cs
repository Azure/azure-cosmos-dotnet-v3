//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    internal sealed class HttpTimeoutPolicyForPartitionFailover : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeoutForQuery = new HttpTimeoutPolicyForPartitionFailover(isQuery: true);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeoutForReads = new HttpTimeoutPolicyForPartitionFailover(isQuery: false);
        private readonly bool isQuery;
        private static readonly string Name = nameof(HttpTimeoutPolicyDefault);

        private HttpTimeoutPolicyForPartitionFailover(bool isQuery)
        {
            this.isQuery = isQuery;
        }

        // Timeouts and delays are based on the following rationale:
        // For reads: 3 agressive attempts with timeouts of .5s, 1s, and 5s respectively.
        // For queries: 3 attempts with timeouts of 5s, 5s, and 10s respectively.
        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelaysForReads = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(1), TimeSpan.Zero),
            (TimeSpan.FromSeconds(5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
        };

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelaysForQueries = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(10), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyForPartitionFailover.Name;

        public override int TotalRetryCount => this.isQuery ? this.TimeoutsAndDelaysForQueries.Count : this.TimeoutsAndDelaysForReads.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.isQuery ? this.TimeoutsAndDelaysForQueries.GetEnumerator() : this.TimeoutsAndDelaysForReads.GetEnumerator();
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return false;
        }

        public override bool ShouldThrow503OnTimeout => true;
    }
}
