//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is an interface to allow a custom serializer to be used by the CosmosClient
    /// </summary>
    internal class CosmosSerializerCore
    {
        private static readonly CosmosSerializer propertiesSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());
        private readonly CosmosSerializer customSerializer;
        private readonly CosmosSerializer sqlQuerySpecSerializer;

        internal CosmosSerializerCore()
            : this(customSerializer: null)
        {
        }

        internal CosmosSerializerCore(
            CosmosSerializer customSerializer)
        {
            if (customSerializer == null)
            {
                this.customSerializer = null;
                this.sqlQuerySpecSerializer = null;
            }
            else
            {
                this.customSerializer = customSerializer;
                this.sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                    cosmosSerializer: this.customSerializer,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
            }
        }

        internal static CosmosSerializerCore Create(
            CosmosSerializer customSerializer,
            CosmosSerializationOptions serializationOptions)
        {
            if (customSerializer != null && serializationOptions != null)
            {
                throw new ArgumentException("Customer serializer and serialization options can not be set at the same time.");
            }

            if (serializationOptions != null)
            {
                customSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(serializationOptions));
            }

            return new CosmosSerializerCore(customSerializer);
        }

        internal T FromStream<T>(Stream stream)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>();
            return serializerCore.FromStream<T>(stream);
        }

        internal Stream ToStream<T>(T input)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>();
            return serializerCore.ToStream<T>(input);
        }

        internal Stream ToStreamSqlQuerySpec(SqlQuerySpec input, ResourceType resourceType)
        {
            CosmosSerializer serializer = CosmosSerializerCore.propertiesSerializer;
            if (this.customSerializer != null &&
                resourceType != ResourceType.Offer)
            {
                serializer = this.sqlQuerySpecSerializer;
            }

            return serializer.ToStream<SqlQuerySpec>(input);
        }

        internal IEnumerable<T> FromFeedResponseStream<T>(
            Stream stream,
            ResourceType resourceType)
        {
            CosmosArray cosmosArray = CosmosElementSerializer.ToCosmosElements(
                    stream,
                    resourceType);

            return CosmosElementSerializer.GetResources<T>(
               cosmosArray: cosmosArray,
               serializerCore: this);
        }

        private CosmosSerializer GetSerializer<T>(ResourceType resourceType = ResourceType.Unknown)
        {
            if (this.customSerializer == null)
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            Type inputType = typeof(T);
            if (inputType == typeof(AccountProperties) ||
                inputType == typeof(DatabaseProperties) ||
                inputType == typeof(ContainerProperties) ||
                inputType == typeof(PermissionProperties) ||
                inputType == typeof(StoredProcedureProperties) ||
                inputType == typeof(TriggerProperties) ||
                inputType == typeof(UserDefinedFunctionProperties) ||
                inputType == typeof(UserProperties) ||
                inputType == typeof(ConflictProperties) ||
                inputType == typeof(ThroughputProperties) ||
                inputType == typeof(OfferV2) ||
                inputType == typeof(PartitionedQueryExecutionInfo) ||
                (resourceType == ResourceType.Offer && inputType == typeof(SqlQuerySpec)))
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            Debug.Assert(inputType != typeof(SqlQuerySpec), "SqlQuerySpec to stream must use the SqlQuerySpec override");
            Debug.Assert(inputType.IsPublic || inputType.IsNested, $"User serializer is being used for internal type:{inputType.FullName}.");

            return this.customSerializer;
        }
    }
}
