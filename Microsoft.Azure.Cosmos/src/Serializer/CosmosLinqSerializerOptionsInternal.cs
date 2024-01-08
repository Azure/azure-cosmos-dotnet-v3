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
            if (customCosmosSerializer is CosmosLinqSerializer customQueryCosmosSerializer)
            {
                if (cosmosLinqSerializerOptions.PropertyNamingPolicy != CosmosPropertyNamingPolicy.Default)
                {
                    throw new InvalidOperationException($"CosmosPropertyNamingPolicy must be CosmosPropertyNamingPolicy.Default if using custom serializer for LINQ translations. See https://aka.ms/CosmosDB/dotnetlinq for more information.");
                }

                return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, customQueryCosmosSerializer);
            }
            else
            {
                return new CosmosLinqSerializerOptionsInternal(cosmosLinqSerializerOptions, null);
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
        /// User defined customer serializer, if one exists. 
        /// Otherwise set to null.
        /// </summary>
        public CosmosLinqSerializer CustomCosmosLinqSerializer { get; }
    }
}
