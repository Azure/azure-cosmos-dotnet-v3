//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// Timeout policy for inference service requests without retry on timeout.
    /// This policy allows for future expansion including handling errors and cross-regional retries.
    /// </summary>
    internal sealed class HttpTimeoutPolicyInference : HttpTimeoutPolicy
    {
        private static readonly string Name = nameof(HttpTimeoutPolicyInference);
        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> timeoutsAndDelays;

        /// <summary>
        /// Creates an instance with default timeout of 5 seconds and no retries.
        /// </summary>
        public static readonly HttpTimeoutPolicyInference InstanceDefault = new HttpTimeoutPolicyInference(TimeSpan.FromSeconds(5));

        private HttpTimeoutPolicyInference(TimeSpan requestTimeout)
        {
            // No retries on timeout - single attempt only
            this.timeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
            {
                (requestTimeout, TimeSpan.Zero)
            };
        }

        /// <summary>
        /// Creates a custom timeout policy for inference service with the specified timeout.
        /// </summary>
        /// <param name="requestTimeout">The timeout to use for each request attempt.</param>
        /// <returns>A new HttpTimeoutPolicyInference instance.</returns>
        public static HttpTimeoutPolicyInference Create(TimeSpan requestTimeout)
        {
            return new HttpTimeoutPolicyInference(requestTimeout);
        }

        public override string TimeoutPolicyName => HttpTimeoutPolicyInference.Name;

        public override int TotalRetryCount => this.timeoutsAndDelays.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.timeoutsAndDelays.GetEnumerator();
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            // For now, no response-based retries
            // Future expansion: Add retry logic for specific HTTP status codes (e.g., 503, 429)
            return false;
        }

        public override bool ShouldThrow503OnTimeout => false;
    }
}
