//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// Control plane hot path policy with a reduced number of attempts (5s, then 65s).
    /// Selected instead of <see cref="HttpTimeoutPolicyControlPlaneRetriableHotPath"/> when the
    /// reduced control plane retries flag is enabled.
    /// </summary>
    internal sealed class HttpTimeoutPolicyControlPlaneReducedRetriesHotPath : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyControlPlaneReducedRetriesHotPath(false);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeout = new HttpTimeoutPolicyControlPlaneReducedRetriesHotPath(true);
        private static readonly string Name = nameof(HttpTimeoutPolicyControlPlaneReducedRetriesHotPath);

        private readonly bool shouldThrow503OnTimeout;

        private HttpTimeoutPolicyControlPlaneReducedRetriesHotPath(bool shouldThrow503OnTimeout)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(65), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyControlPlaneReducedRetriesHotPath.Name;

        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                return false;
            }

            return responseMessage.StatusCode == System.Net.HttpStatusCode.RequestTimeout;
        }

        public override bool ShouldThrow503OnTimeout => this.shouldThrow503OnTimeout;
    }
}
