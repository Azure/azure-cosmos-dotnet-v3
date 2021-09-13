﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal abstract class Page<TState>
        where TState : State
    {
        protected static readonly ImmutableHashSet<string> BannedHeadersBase = new HashSet<string>()
        {
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge,
            Microsoft.Azure.Documents.HttpConstants.HttpHeaders.ActivityId,
        }.ToImmutableHashSet();

        private static readonly ImmutableDictionary<string, string> EmptyDictionary = new Dictionary<string, string>().ToImmutableDictionary();

        protected Page(
            double requestCharge,
            string activityId,
            IReadOnlyDictionary<string, string> additionalHeaders,
            TState state)
        {
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId;
            this.State = state;

#if DEBUG
            // Only do the additional header validation on debug.
            // This causes a significant impact to performance and is not necessary for release scenarios.
            if (additionalHeaders != null)
            {
                foreach (string key in additionalHeaders.Keys)
                {
                    if (BannedHeadersBase.Contains(key) || this.DerivedClassBannedHeaders.Contains(key))
                    {
                        throw new ArgumentOutOfRangeException($"'{key}' is not allowed as an additional header, since it's already defined in the schema.");
                    }
                }
            }
#endif

            this.AdditionalHeaders = additionalHeaders == null ? EmptyDictionary : additionalHeaders.ToImmutableDictionary();
        }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ImmutableDictionary<string, string> AdditionalHeaders { get; }

        public TState State { get; }

        protected abstract ImmutableHashSet<string> DerivedClassBannedHeaders { get; }
    }
}
