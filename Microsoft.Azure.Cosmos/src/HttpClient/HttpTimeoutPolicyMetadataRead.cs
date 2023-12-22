//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// Timeout policy for metadata read requests.
    /// </summary>
    internal sealed class HttpTimeoutPolicyMetadataRead : HttpTimeoutPolicy
    {
        /// <summary>
        /// A read-only static instance of <see cref="HttpTimeoutPolicyMetadataRead"/> configuring the
        /// policy not to throw a 503 on request timeout event.
        /// </summary>
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyMetadataRead(false);

        /// <summary>
        /// A read-only static instance of <see cref="HttpTimeoutPolicyMetadataRead"/> configuring the
        /// policy to throw a 503 on request timeout event.
        /// </summary>
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeout = new HttpTimeoutPolicyMetadataRead(true);

        /// <summary>
        /// A static read-only string defining the name of the timeout policy.
        /// </summary>
        private static readonly string Name = nameof(HttpTimeoutPolicyMetadataRead);

        /// <summary>
        /// A read-only boolean flag indicating if the retry policy will throw a 503 exception on timeout.
        /// </summary>
        private readonly bool shouldThrow503OnTimeout;

        /// <summary>
        /// A private construcotr to initialize an instance of <see cref="HttpTimeoutPolicyMetadataRead"/>.
        /// </summary>
        /// <param name="shouldThrow503OnTimeout"></param>
        private HttpTimeoutPolicyMetadataRead(bool shouldThrow503OnTimeout)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
        }

        /// <summary>
        /// A read-only list containing the request timeout values configured and the retry delay associated with each timeout value.
        /// </summary>
        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(3), TimeSpan.Zero),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(65), TimeSpan.Zero), 
        };

        /// <inheritdoc/>
        public override string TimeoutPolicyName => HttpTimeoutPolicyMetadataRead.Name;

        /// <inheritdoc/>
        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

        /// <inheritdoc/>
        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        // Assume that it is not safe to retry unless it is a get method.
        // Create and other operations could have succeeded even though a timeout occurred.
        public override bool IsSafeToRetry(HttpMethod httpMethod)
        {
            return httpMethod == HttpMethod.Get;
        }

        /// <inheritdoc/>
        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return this.IsSafeToRetry(requestHttpMethod)
                && responseMessage.StatusCode == System.Net.HttpStatusCode.RequestTimeout;
        }

        /// <inheritdoc/>
        public override bool ShouldThrow503OnTimeout => this.shouldThrow503OnTimeout;
    }
}
