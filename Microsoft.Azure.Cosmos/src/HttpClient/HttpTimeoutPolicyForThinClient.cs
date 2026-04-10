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
        public bool isPointRead;
        private static readonly string Name = nameof(HttpTimeoutPolicyForThinClient);
        public static readonly HttpTimeoutPolicy InstanceShouldRetryAndThrow503OnTimeoutForPointReads = new HttpTimeoutPolicyForThinClient(shouldThrow503OnTimeout: true, shouldRetry: true, isPointRead: true);
        public static readonly HttpTimeoutPolicy InstanceShouldRetryAndThrow503OnTimeoutForNonPointReads = new HttpTimeoutPolicyForThinClient(shouldThrow503OnTimeout: true, shouldRetry: true, isPointRead: false);
        public static readonly HttpTimeoutPolicy InstanceShouldNotRetryAndThrow503OnTimeoutForWrites = new HttpTimeoutPolicyForThinClient(shouldThrow503OnTimeout: true, shouldRetry: false, isPointRead: false);

        private HttpTimeoutPolicyForThinClient(
            bool shouldThrow503OnTimeout,
            bool shouldRetry,
            bool isPointRead)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
            this.shouldRetry = shouldRetry;
            this.isPointRead = isPointRead;
        }

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

        public override string TimeoutPolicyName => HttpTimeoutPolicyForThinClient.Name;

        public override int TotalRetryCount => this.isPointRead ? this.TimeoutsAndDelaysForPointReads.Count : this.TimeoutsAndDelaysForNonPointReads.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.isPointRead ? this.TimeoutsAndDelaysForPointReads.GetEnumerator() : this.TimeoutsAndDelaysForNonPointReads.GetEnumerator();
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

            if (!this.shouldRetry)
            {
                return false;
            }

            return true;
        }

        public override bool ShouldThrow503OnTimeout => this.shouldThrow503OnTimeout;
    }
}
