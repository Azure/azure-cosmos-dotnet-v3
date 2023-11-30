// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// This class stores user-provided LINQ Serialization Properties.
    /// </summary>
    internal sealed class CosmosLinqSerializerOptionsInternal
    {
        /// <summary>
        /// Creates an instance of CosmosSerializationOptionsInternal.
        /// </summary>
        public static CosmosLinqSerializerOptionsInternal Create(CosmosLinqSerializerOptions cosmosLinqSerializerOptions, CosmosSerializer customCosmosSerializer)
        {
            switch (cosmosLinqSerializerOptions.LinqSerializerType)
            {
                case LinqSerializerType.CustomCosmosSerializer:
                {
                    if (customCosmosSerializer == null)
                    {
                        throw new InvalidOperationException($"Must provide CustomCosmosSerializer if selecting linqSerializerOptions.CustomCosmosSerializer.");
                    }

                    if (cosmosLinqSerializerOptions.PropertyNamingPolicy != CosmosPropertyNamingPolicy.Default)
                    {
                        throw new InvalidOperationException($"CosmosPropertyNamingPolicy must be CosmosPropertyNamingPolicy.Default if selecting linqSerializerOptions.CustomCosmosSerializer.");
                    }

                    return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, customCosmosSerializer);
                }
                case LinqSerializerType.Default:
                {
                    return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, null);
                }
                default:
                {
                    throw new InvalidOperationException("Unsupported LinqSerializerType value.");
                }
            }
        }

        private CosmosLinqSerializerOptionsInternal(CosmosLinqSerializerOptions cosmosLinqSerializerOptions, CosmosSerializer customCosmosSerializer)
        {
            this.CosmosLinqSerializerOptions = cosmosLinqSerializerOptions;
            this.CustomCosmosSerializer = customCosmosSerializer;
        }

        /// <summary>
        /// User-provided CosmosLinqSerializerOptions.
        /// </summary>
        public readonly CosmosLinqSerializerOptions CosmosLinqSerializerOptions;

        /// <summary>
        /// User defined customer serializer, if LinqSerializerType is CustomCosmosSerializer. 
        /// Otherwise set to null;
        /// </summary>
        public readonly CosmosSerializer CustomCosmosSerializer;
    }
}
