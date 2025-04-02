﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;

    internal sealed class HttpTimeoutPolicyForPartitionFailover : HttpTimeoutPolicy
    {
        public static readonly HttpTimeoutPolicy Instance = new HttpTimeoutPolicyForPartitionFailover(false);
        public static readonly HttpTimeoutPolicy InstanceShouldThrow503OnTimeout = new HttpTimeoutPolicyForPartitionFailover(true);
        public bool shouldThrow503OnTimeout;
        private static readonly string Name = nameof(HttpTimeoutPolicyDefault);

        private HttpTimeoutPolicyForPartitionFailover(bool shouldThrow503OnTimeout)
        {
            this.shouldThrow503OnTimeout = shouldThrow503OnTimeout;
        }

        private readonly IReadOnlyList<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutsAndDelays = new List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)>()
        {
            (TimeSpan.FromSeconds(.5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(.5), TimeSpan.Zero),
            (TimeSpan.FromSeconds(1), TimeSpan.Zero),
        };

        public override string TimeoutPolicyName => HttpTimeoutPolicyForPartitionFailover.Name;

        public override int TotalRetryCount => this.TimeoutsAndDelays.Count;

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

        public override bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage)
        {
            return false;
        }

        public override bool ShouldThrow503OnTimeout => this.shouldThrow503OnTimeout;
    }
}
