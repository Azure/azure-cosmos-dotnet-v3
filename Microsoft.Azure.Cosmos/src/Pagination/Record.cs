// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Documents;

    internal sealed class Record
    {
        public Record(
            ResourceId resourceIdentifier,
            DateTime timestamp,
            string identifier,
            CosmosObject payload)
        {
            this.ResourceIdentifier = resourceIdentifier;
            this.Timestamp = timestamp.Kind != DateTimeKind.Utc ? throw new ArgumentOutOfRangeException("date time must be utc") : timestamp;
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ResourceId ResourceIdentifier { get; }

        public DateTime Timestamp { get; }

        public string Identifier { get; }

        public CosmosObject Payload { get; }

        public CosmosObject ToDocument()
        {
            Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
            {
                ["_rid"] = CosmosString.Create(this.ResourceIdentifier.ToString()),
                ["_ts"] = CosmosNumber64.Create(this.Timestamp.Ticks),
                ["id"] = CosmosString.Create(this.Identifier)
            };

            foreach (KeyValuePair<string, CosmosElement> property in this.Payload)
            {
                keyValuePairs[property.Key] = property.Value;
            }

            return CosmosObject.Create(keyValuePairs);
        }
    }
}
