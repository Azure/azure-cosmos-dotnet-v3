//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    internal sealed class HttpTimeoutPolicyForThinClient : HttpTimeoutPolicy
    {
        public bool shouldRetry;
        public bool shouldThrow503OnTimeout;
        private static readonly string Name = nameof(HttpTimeoutPolicyForThinClient);
        public static readonly HttpTimeoutPolicy InstanceShouldRetryAndThrow503OnTimeout = new HttpTimeoutPolicyForThinClient(true, true);
        public static readonly HttpTimeoutPolicy InstanceShouldNotRetryAndThrow503OnTimeout = new HttpTimeoutPolicyForThinClient(true, false);

        private HttpTimeoutPolicyForThinClient(
            bool shouldThrow503OnTimeout,
            bool shouldRetry)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
            this.shouldRetry = shouldRetry;
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(.5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(1), TimeSpan.Zero),
            (TimeSpan.FromSeconds(5), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyForThinClient.Name;

        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        public override bool IsSafeToRetry(HttpMethod httpMethod)
        {
            return this.shouldRetry;
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        { 
            if (responseMessage == null)
            {
                return false;
            }

            if (responseMessage.StatusCode != System.Net.HttpStatusCode.RequestTimeout)
            {
                return false;
            }

            if (!this.IsSafeToRetry(requestHttpMethod))
            {
                return false;
            }

            return true;
        }

        public override bool ShouldThrow503OnTimeout => this.shouldThrow503OnTimeout;
    }
}
