//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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

        public T FromStream<T>(Stream stream)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>();
            return serializerCore.FromStream<T>(stream);
        }

        public Stream ToStream<T>(T input)
        {
            CosmosSerializer serializerCore = this.GetSerializer<T>();
            return serializerCore.ToStream<T>(input);
        }

        private CosmosSerializer GetSerializer<T>()
        {
            if (this.customSerializer == null)
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            Type inputType = typeof(T);
            if (inputType == typeof(CosmosFeedResponseUtil<>))
            {
                Debug.Assert(inputType.GenericTypeArguments.Length == 0);
            }

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
    }
}
