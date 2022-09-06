//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;

    internal sealed class HttpTimeoutPolicyControlPlaneRetriableHotPath : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyControlPlaneRetriableHotPath();
        private static readonly string Name = nameof(HttpTimeoutPolicyControlPlaneRetriableHotPath);

        private HttpTimeoutPolicyControlPlaneRetriableHotPath()
        {
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(.5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(65), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyControlPlaneRetriableHotPath.Name;

        public override TimeSpan MaximumRetryTimeLimit => CosmosHttpClient.GatewayRequestTimeout;

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
    }
}
