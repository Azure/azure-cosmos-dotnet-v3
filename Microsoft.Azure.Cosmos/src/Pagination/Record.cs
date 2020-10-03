// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class Record
    {
        public Record(
            ResourceIdentifier resourceIdentifier,
            long timestamp,
            string identifier,
            CosmosObject payload)
        {
            this.ResourceIdentifier = resourceIdentifier;
            this.Timestamp = timestamp < 0 ? throw new ArgumentOutOfRangeException(nameof(timestamp)) : timestamp;
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ResourceIdentifier ResourceIdentifier { get; }

        public long Timestamp { get; }

        public string Identifier { get; }

        public CosmosObject Payload { get; }

        public static Record Create(ResourceIdentifier previousResourceIdentifier, CosmosObject payload)
        {
            ResourceIdentifier resourceId = new ResourceIdentifier(
                previousResourceIdentifier.Offer, previousResourceIdentifier.Database,
                previousResourceIdentifier.DocumentCollection, previousResourceIdentifier.StoredProcedure,
                previousResourceIdentifier.Trigger, previousResourceIdentifier.UserDefinedFunction,
                previousResourceIdentifier.Conflict, previousResourceIdentifier.Document + 1,
                previousResourceIdentifier.PartitionKeyRange, previousResourceIdentifier.User,
                previousResourceIdentifier.ClientEncryptionKey, previousResourceIdentifier.UserDefinedType,
                previousResourceIdentifier.Permission, previousResourceIdentifier.Attachment,
                previousResourceIdentifier.Schema, previousResourceIdentifier.Snapshot,
                previousResourceIdentifier.RoleAssignment, previousResourceIdentifier.RoleDefinition);

            return new Record(
                resourceId,
                DateTime.UtcNow.Ticks,
                Guid.NewGuid().ToString(),
                payload);
        }
    }
}
