﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
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
    }
}
