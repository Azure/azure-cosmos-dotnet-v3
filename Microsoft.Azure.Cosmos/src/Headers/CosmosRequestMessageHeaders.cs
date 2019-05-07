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

    /// <summary>
    /// Http headers in a <see cref="CosmosRequestMessage"/>.
    /// </summary>
    public class CosmosRequestMessageHeaders : CosmosMessageHeadersBase
    {
        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.PartitionKey)]
        internal string PartitionKey
        {
            get
            {
                return this.CosmosMessageHeaders.PartitionKey;
            }
            set
            {
                this.CosmosMessageHeaders.PartitionKey = value;
            }
        }

        internal string AuthorizationToken
        {
            get => this.CosmosMessageHeaders.AuthorizationToken;
            set => this.CosmosMessageHeaders.AuthorizationToken = value;
        }

        [CosmosKnownHeaderAttribute(HeaderName = WFConstants.BackendHeaders.PartitionKeyRangeId)]
        internal string PartitionKeyRangeId
        {
            get => this.CosmosMessageHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId];
            set => this.CosmosMessageHeaders[WFConstants.BackendHeaders.PartitionKeyRangeId] = value;
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.Continuation)]
        internal string Continuation
        { 
            get => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation];
            set => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.Continuation] = value;
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.IsUpsert)]
        internal string IsUpsert
        { 
            get => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.IsUpsert];
            set => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.IsUpsert] = value;
        }

        [CosmosKnownHeaderAttribute(HeaderName = HttpConstants.HttpHeaders.OfferThroughput)]
        internal string OfferThroughput {
            get => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.OfferThroughput];
            set => this.CosmosMessageHeaders[HttpConstants.HttpHeaders.OfferThroughput] = value;

        }

        private static KeyValuePair<string, PropertyInfo>[] knownHeaderProperties = CosmosMessageHeadersInternal.GetHeaderAttributes<CosmosRequestMessageHeaders>();

        private static Dictionary<string, CosmosCustomHeader> headers = new Dictionary<string, CosmosCustomHeader>(
            CosmosRequestMessageHeaders.knownHeaderProperties.Length,
            StringComparer.OrdinalIgnoreCase);

        internal override Dictionary<string, CosmosCustomHeader> CreateKnownDictionary()
        {
            return CosmosRequestMessageHeaders.headers;
        }
    }
}