//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core;
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

        public enum SerializerType
        {
            UserType,
            InternalType,
            SqlQuerySpecType,
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

        public T FromStream<T>(Stream stream, SerializerType serializerType)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>(serializerType);
            return serializerCore.FromStream<T>(stream);
        }

        public Collection<T> FromFeedResponseStream<T>(Stream stream)
        {
            CosmosSerializer serializerCore = this.GetFeedResponseSerializer<T>();
            return serializerCore.FromStream<CosmosFeedResponseUtil<T>>(stream).Data;
        }

        public Stream ToStream<T>(T input, SerializerType serializerType)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>(serializerType);
            return serializerCore.ToStream<T>(input);
        }

        private CosmosSerializer GetFeedResponseSerializer<T>()
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
                inputType == typeof(OfferV2))
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            if (inputType == typeof(SqlQuerySpec))
            {
                return this.sqlQuerySpecSerializer;
            }

            Debug.Assert(inputType.IsPublic, $"User serializer is being used for internal type:{inputType.FullName}.");

            return this.customSerializer;
        }

        private CosmosSerializer GetSerializer<T>(SerializerType serializerType)
        {
            if (this.customSerializer == null)
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            if (serializerType == SerializerType.InternalType)
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            if (serializerType == SerializerType.SqlQuerySpecType)
            {
                return this.sqlQuerySpecSerializer;
            }

            // User serializer is responsible for all item operation serialization.
            // This includes any public SDK types.
            Debug.Assert(typeof(T).IsPublic, $"User serializer is being used for internal type:{typeof(T).FullName}.");
            return this.customSerializer;
        }

        
    }
}
