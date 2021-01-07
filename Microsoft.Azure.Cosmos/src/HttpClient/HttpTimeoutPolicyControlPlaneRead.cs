//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    internal sealed class HttpTimeoutPolicyControlPlaneRead : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyControlPlaneRead();
        private static readonly string Name = nameof(HttpTimeoutPolicyControlPlaneRead);

        private HttpTimeoutPolicyControlPlaneRead()
        {
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)),
            (TimeSpan.FromSeconds(20), TimeSpan.Zero), 
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyControlPlaneRead.Name;

        public override TimeSpan MaximumRetryTimeLimit => CosmosHttpClient.GatewayRequestTimeout;

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
    }
}
