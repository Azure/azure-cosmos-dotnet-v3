// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;

    internal sealed class Record
    {
        public Record(
            ResourceId resourceIdentifier,
            long timestamp,
            string identifier,
            CosmosObject payload)
        {
            this.ResourceIdentifier = resourceIdentifier;
            this.Timestamp = timestamp < 0 ? throw new ArgumentOutOfRangeException(nameof(timestamp)) : timestamp;
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ResourceId ResourceIdentifier { get; }

        public long Timestamp { get; }

        public string Identifier { get; }

        public CosmosObject Payload { get; }

        public static Record Create(ResourceId previousResourceIdentifier, CosmosObject payload)
        {
            const string dummyRidString = "AYIMAMmFOw8YAAAAAAAAAA==";
            ResourceId resourceId = ResourceId.Parse(dummyRidString);
            PropertyInfo prop = resourceId
                .GetType()
                .GetProperty("Document", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(resourceId, previousResourceIdentifier.Document + 1);

            return new Record(
                resourceId,
                DateTime.UtcNow.Ticks,
                Guid.NewGuid().ToString(),
                payload);
        }
    }
}
