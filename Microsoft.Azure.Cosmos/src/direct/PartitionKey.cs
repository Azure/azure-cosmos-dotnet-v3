//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class PartitionKey
    {
        /// <summary>
        /// Instantiate a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <remarks>
        /// Private constructor used internal to create an instance from a JSON string.
        /// </remarks>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        private PartitionKey()
        {
        }

        /// <summary>
        /// Instantiate a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <param name="keyValue">
        /// The value of the document property that is specified as the partition key when a collection is created.
        /// </param>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        public PartitionKey(object keyValue)
        {
            this.InternalKey = PartitionKeyInternal.FromObjectArray(new object[] { keyValue }, true);
        }

        /// <summary>
        /// Instantiate a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <param name="keyValues">
        ///  Values of the document property that are specified as partition keys when a collection is created.
        /// </param>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        internal PartitionKey(object[] keyValues)
        {
            this.InternalKey = PartitionKeyInternal.FromObjectArray( keyValues ?? new object[] { null }, true);
        }

        /// <summary>
        /// Instantiate a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <param name="keyValue">
        /// The value of the document property that is specified as the partition key
        /// when a collection is created, in serialized JSON form.
        /// </param>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        public static PartitionKey FromJsonString(string keyValue)
        {
            if (string.IsNullOrEmpty(keyValue))
            {
                throw new ArgumentException("keyValue must not be null or empty.");
            }

            return new PartitionKey { InternalKey = PartitionKeyInternal.FromJsonString(keyValue) };
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <remarks>
        /// The returned object represents a partition key value that allows creating and accessing documents
        /// without a value for partition key
        /// </remarks>
        public static PartitionKey None => new PartitionKey { InternalKey = PartitionKeyInternal.None };

        /// <summary>
        /// The tag name to use in the documents for specifying a partition key value
        /// when inserting such documents into a migrated collection
        /// </summary>
        public const string SystemKeyName = "_partitionKey";

        /// <summary>
        /// The partition key path in the collection definition for migrated collections
        /// </summary>
        public const string SystemKeyPath = "/_partitionKey";

        /// <summary>
        /// Instantiate a new instance of the <see cref="PartitionKey"/> object.
        /// </summary>
        /// <param name="keyValue">
        /// The value of the document property that is specified as the partition key
        /// when a collection is created, in PartitionKeyInternal format.
        /// </param>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        internal static PartitionKey FromInternalKey(PartitionKeyInternal keyValue)
        {
            if (keyValue == null)
            {
                throw new ArgumentException("keyValue must not be null or empty.");
            }

            return new PartitionKey { InternalKey = keyValue };
        }


        /// <summary>
        /// Gets the internal <see cref="PartitionKeyInternal"/> object;
        /// </summary>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        internal PartitionKeyInternal InternalKey { get; private set; }

        /// <summary>
        /// Override the base ToString method to output the value of each key component, separated by a space.
        /// </summary>
        /// <returns>The string representation of all the key component values.</returns>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        public override string ToString()
        {
            return this.InternalKey.ToJsonString();
        }

        /// <summary>
        /// Overrides the Equal operator for object comparisons between two instances of <see cref="PartitionKey"/>.
        /// </summary>
        /// <param name="other">The object to compare with.</param>
        /// <returns>True if two object instance are considered equal.</returns>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        public override bool Equals(object other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            PartitionKey otherKey = other as PartitionKey;
            return otherKey != null && this.InternalKey.Equals(otherKey.InternalKey);
        }

        /// <summary>
        /// Hash function to return the hash code for the object.
        /// </summary>
        /// <returns>The hash code for this <see cref="PartitionKey"/> instance</returns>
        /// <remarks>
        /// This class represents a partition key value that identifies the target partition of a collection in the Azure Cosmos DB service.
        /// </remarks>
        public override int GetHashCode()
        {
            return this.InternalKey != null ? this.InternalKey.GetHashCode() : base.GetHashCode();
        }
    }
}
