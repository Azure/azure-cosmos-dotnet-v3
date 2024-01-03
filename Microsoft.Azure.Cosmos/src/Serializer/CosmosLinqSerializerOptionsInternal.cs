// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// This class stores user-provided LINQ Serialization Properties.
    /// </summary>
    internal sealed class CosmosLinqSerializerOptionsInternal
    {
        /// <summary>
        /// Creates an instance of CosmosSerializationOptionsInternal.
        /// </summary>
        public static CosmosLinqSerializerOptionsInternal Create(
            CosmosLinqSerializerOptions cosmosLinqSerializerOptions, 
            CosmosSerializer customCosmosSerializer)
        {
            switch (cosmosLinqSerializerOptions.LinqSerializerType)
            {
                case CosmosLinqSerializerType.Custom:
                {
                    if (customCosmosSerializer == null)
                    {
                        throw new InvalidOperationException($"Must provide CosmosLinqSerializer if selecting CosmosLinqSerializerType.Custom.");
                    }

                    if (cosmosLinqSerializerOptions.PropertyNamingPolicy != CosmosPropertyNamingPolicy.Default)
                    {
                        throw new InvalidOperationException($"CosmosPropertyNamingPolicy must be CosmosPropertyNamingPolicy.Default if selecting CosmosLinqSerializerType.Custom.");
                    }

                    if (customCosmosSerializer is CosmosLinqSerializer customQueryCosmosSerializer)
                    {
                        return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, customQueryCosmosSerializer);
                    }

                    throw new InvalidOperationException($"CosmosSerializer must implement CosmosLinqSerializer if selecting CosmosLinqSerializerType.Custom.");
                }
                case CosmosLinqSerializerType.Default:
                {
                    return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, null);
                }
                default:
                {
                    throw new InvalidOperationException("Unsupported CosmosLinqSerializerType value.");
                }
            }
        }

        private CosmosLinqSerializerOptionsInternal(
            CosmosLinqSerializerOptions cosmosLinqSerializerOptions, 
            CosmosLinqSerializer customCosmosLinqSerializer)
        {
            this.CosmosLinqSerializerOptions = cosmosLinqSerializerOptions;
            this.CustomCosmosLinqSerializer = customCosmosLinqSerializer;
        }

        /// <summary>
        /// User-provided CosmosLinqSerializerOptions.
        /// </summary>
        public CosmosLinqSerializerOptions CosmosLinqSerializerOptions { get; }

        /// <summary>
        /// User defined customer serializer, if CosmosLinqSerializerType is CustomCosmosLinqSerializer. 
        /// Otherwise set to null.
        /// </summary>
        public CosmosLinqSerializer CustomCosmosLinqSerializer { get; }
    }
}
