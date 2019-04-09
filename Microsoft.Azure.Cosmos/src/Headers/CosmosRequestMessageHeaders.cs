//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Http headers in a <see cref="CosmosRequestMessage"/>.
    /// </summary>
    public class CosmosRequestMessageHeaders : CosmosMessageHeadersBase
    {
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.PartitionKey)]
        internal string PartitionKey { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = WFConstants.BackendHeaders.PartitionKeyRangeId)]
        internal string PartitionKeyRangeId { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Continuation)]
        internal string Continuation { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.IsUpsert)]
        internal string IsUpsert { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.OfferThroughput)]
        internal string OfferThroughput { get; set; }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.IfNoneMatch)]
        internal string IfNoneMatch { get; set; }

        private static KeyValuePair<string, PropertyInfo>[] knownHeaderProperties = CosmosMessageHeadersInternal.GetHeaderAttributes<CosmosRequestMessageHeaders>();

        internal override Dictionary<string, CosmosCustomHeader> CreateKnownDictionary()
        {
            return CosmosRequestMessageHeaders.knownHeaderProperties.ToDictionary(
                    knownProperty => knownProperty.Key,
                    knownProperty => new CosmosCustomHeader(
                            () => (string)knownProperty.Value.GetValue(this),
                            (string value) => { knownProperty.Value.SetValue(this, value); }
                        )
                , StringComparer.OrdinalIgnoreCase);
        }
    }
}