//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    internal sealed class HttpTimeoutPolicyMetadataRead : HttpTimeoutPolicy
    {
        private static readonly string Name = nameof(HttpTimeoutPolicyMetadataRead);
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyMetadataRead(false);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeout = new HttpTimeoutPolicyMetadataRead(true);
        public bool shouldThrow503OnTimeout;

        private HttpTimeoutPolicyMetadataRead(bool shouldThrow503OnTimeout)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(3), TimeSpan.Zero),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(65), TimeSpan.Zero), 
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyMetadataRead.Name;

        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        // This is for control plane reads which should always be safe to retry on.
        public override bool IsSafeToRetry(HttpMethod httpMethod)
        {
            return true;
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return false;
        }

        public override bool ShouldThrow503OnTimeout => true;
    }
}
