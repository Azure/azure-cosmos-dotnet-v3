//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;

    internal sealed class HttpTimeoutPolicyNoRetry : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyNoRetry();
        private static readonly string Name = nameof(HttpTimeoutPolicyNoRetry);

        private HttpTimeoutPolicyNoRetry()
        {
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(65), TimeSpan.Zero)
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyNoRetry.Name;

        public override TimeSpan MaximumRetryTimeLimit => TimeSpan.Zero;

        public override int TotalRetryCount => 0;

        public override IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator()
        {
            return this.TimeoutsAndDelays.GetEnumerator();
        }

        // Always Unsafe to retry
        public override bool IsSafeToRetry(HttpMethod httpMethod)
        {
            return false;
        }

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return false;
        }
    }
}
