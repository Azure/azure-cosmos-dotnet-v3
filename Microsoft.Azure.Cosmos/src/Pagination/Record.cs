// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class Record
    {
        private Record(long resourceIdentifier, long timestamp, Guid identifier, CosmosObject payload)
        {
            this.ResourceIdentifier = resourceIdentifier < 0 ? throw new ArgumentOutOfRangeException(nameof(resourceIdentifier)) : resourceIdentifier;
            this.Timestamp = timestamp < 0 ? throw new ArgumentOutOfRangeException(nameof(timestamp)) : timestamp;
            this.Identifier = identifier;
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public long ResourceIdentifier { get; }

        public long Timestamp { get; }

        public Guid Identifier { get; }

        public CosmosObject Payload { get; }

        public static Record Create(long previousResourceIdentifier, CosmosObject payload)
        {
            return new Record(previousResourceIdentifier + 1, DateTime.UtcNow.Ticks, Guid.NewGuid(), payload);
        }
    }
}
