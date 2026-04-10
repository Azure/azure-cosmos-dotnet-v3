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
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeoutForNonPointReads = new HttpTimeoutPolicyForPartitionFailover(isPointRead: false);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeoutForPointReads = new HttpTimeoutPolicyForPartitionFailover(isPointRead: true);
        private readonly bool isPointRead;
        private static readonly string Name = nameof(HttpTimeoutPolicyDefault);

        private HttpTimeoutPolicyForPartitionFailover(bool isPointRead)
        {
            this.isPointRead = isPointRead;
        }

        // Timeouts and delays are based on the following rationale:
        // For point reads: 3 attempts with timeouts of 6s, 6s, and 10s respectively.
        // For non-point reads: 3 attempts with timeouts of 6s, 6s, and 10s respectively.
        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelaysForPointReads = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(10), TimeSpan.Zero),
        };

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelaysForNonPointReads = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(6), TimeSpan.Zero),
            (TimeSpan.FromSeconds(10), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyForPartitionFailover.Name;

        public override int TotalRetryCount => this.isPointRead ? this.TimeoutsAndDelaysForPointReads.Count : this.TimeoutsAndDelaysForNonPointReads.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.isPointRead ? this.TimeoutsAndDelaysForPointReads.GetEnumerator() : this.TimeoutsAndDelaysForNonPointReads.GetEnumerator();
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return false;
        }

        public override bool ShouldThrow503OnTimeout => true;
    }
}
