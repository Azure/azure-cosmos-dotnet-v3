//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal sealed class HttpTimeoutPolicyControlPlaneRetriableHotPath : HttpTimeoutPolicy
    {
        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the default value of the first request timeout after which the request is retried.
        /// </summary>
        private const double firstRetryTimeoutDefault = 500;

        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyControlPlaneRetriableHotPath(false);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeout = new HttpTimeoutPolicyControlPlaneRetriableHotPath(true);
        public bool shouldThrow503OnTimeout;
        private static readonly string Name = nameof(HttpTimeoutPolicyControlPlaneRetriableHotPath);

        private HttpTimeoutPolicyControlPlaneRetriableHotPath(bool shouldThrow503OnTimeout)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
                (TimeSpan.FromMilliseconds(
                        Math.Max(100, Helpers.GetEnvironmentVariable(
                            name: ConfigurationManager.HttpFirstRetryTimeoutValue,
                            defaultValue: firstRetryTimeoutDefault))),
                    TimeSpan.Zero
                ),
                (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
                (TimeSpan.FromSeconds(65), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyControlPlaneRetriableHotPath.Name;

        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        // The hot path should always be safe to retires since it should be retrieving meta data 
        // information that is not idempotent.
        public override bool IsSafeToRetry(HttpMethod httpMethod)
        {
            return true;
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
