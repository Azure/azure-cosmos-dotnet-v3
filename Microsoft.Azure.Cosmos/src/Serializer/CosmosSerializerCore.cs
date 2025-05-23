﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is an interface to allow a custom serializer to be used by the CosmosClient
    /// </summary>
    internal class CosmosSerializerCore
    {
        private readonly bool isBinaryEncodingEnabled;
        private static readonly CosmosSerializer binarySerializer = new CosmosJsonSerializerWrapper(
            new CosmosJsonDotNetSerializer(binaryEncodingEnabled: true));

        private static readonly CosmosSerializer propertiesSerializer = new CosmosJsonSerializerWrapper(
            new CosmosJsonDotNetSerializer());

        private readonly CosmosSerializer customSerializer;
        private readonly CosmosSerializer sqlQuerySpecSerializer;
        private CosmosSerializer patchOperationSerializer;

        internal CosmosSerializerCore(
            CosmosSerializer customSerializer = null)
        {
            this.isBinaryEncodingEnabled = ConfigurationManager.IsBinaryEncodingEnabled();
            if (customSerializer == null)
            {
                this.customSerializer = null;
                // this would allow us to set the JsonConverter and inturn handle Serialized/Stream Query Parameter Value.
                this.sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                    cosmosSerializer: null,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                this.patchOperationSerializer = null;
            }
            else
            {
                this.customSerializer = new CosmosJsonSerializerWrapper(customSerializer);
                this.sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                    cosmosSerializer: this.customSerializer,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                this.patchOperationSerializer = PatchOperationsJsonConverter.CreatePatchOperationsSerializer(
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
                customSerializer = new CosmosJsonSerializerWrapper(
                    new CosmosJsonDotNetSerializer(
                        serializationOptions,
                        binaryEncodingEnabled: ConfigurationManager.IsBinaryEncodingEnabled()));
            }

            return new CosmosSerializerCore(customSerializer);
        }

        internal T FromStream<T>(Stream stream)
        {
            CosmosSerializer serializer = this.GetSerializer<T>();
            return serializer.FromStream<T>(stream);
        }

        internal T[] FromFeedStream<T>(Stream stream)
        {
            CosmosSerializer serializer = this.GetSerializer<T>();
            return serializer.FromStream<T[]>(stream);
        }

        internal Stream ToStream<T>(T input, bool canUseBinaryEncodingForPointOperations = false)
        {
            CosmosSerializer serializer = this.GetSerializer<T>(canUseBinaryEncodingForPointOperations);
            return serializer.ToStream<T>(input);
        }

        internal Stream ToStreamSqlQuerySpec(SqlQuerySpec input, ResourceType resourceType)
        {
            CosmosSerializer serializer = CosmosSerializerCore.propertiesSerializer;

            // All the public types that support query use the custom serializer
            // Internal types like offers will use the default serializer.
            if (resourceType == ResourceType.Database ||
                resourceType == ResourceType.Collection ||
                resourceType == ResourceType.Document ||
                resourceType == ResourceType.Trigger ||
                resourceType == ResourceType.UserDefinedFunction ||
                resourceType == ResourceType.StoredProcedure ||
                resourceType == ResourceType.Permission ||
                resourceType == ResourceType.User ||
                resourceType == ResourceType.Conflict)
            {
                serializer = this.sqlQuerySpecSerializer;
            }

            return serializer.ToStream<SqlQuerySpec>(input);
        }

        internal CosmosSerializer GetCustomOrDefaultSerializer()
        {
            if (this.customSerializer != null)
            {
                return this.customSerializer;
            }

            return this.GetDefaultSerializer();
        }

        private CosmosSerializer GetSerializer<T>(
            bool canUseBinaryEncodingForPointOperations = false)
        {
            Type inputType = typeof(T);
            if (inputType == typeof(PatchSpec))
            {
                this.patchOperationSerializer ??= PatchOperationsJsonConverter.CreatePatchOperationsSerializer(
                        cosmosSerializer: this.customSerializer ?? new CosmosJsonDotNetSerializer(),
                        propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                return this.patchOperationSerializer;
            }

            if (CosmosSerializerCore.IsInputTypeInternal(inputType))
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            if (this.customSerializer == null)
            {
                return this.GetDefaultSerializer(canUseBinaryEncodingForPointOperations);
            }

            if (inputType == typeof(SqlQuerySpec))
            {
                throw new ArgumentException("SqlQuerySpec to stream must use the SqlQuerySpec override");
            }

#if DEBUG
            // This check is used to stop internal developers from deserializing an internal with the user's serialier that doesn't know how to materialize said type.
            string clientAssemblyName = typeof(DatabaseProperties).Assembly.GetName().Name;
            string directAssemblyName = typeof(Documents.PartitionKeyRange).Assembly.GetName().Name;
            string inputAssemblyName = inputType.Assembly.GetName().Name;
            bool inputIsClientOrDirect = string.Equals(inputAssemblyName, clientAssemblyName) || string.Equals(inputAssemblyName, directAssemblyName);
            bool typeIsWhiteListed = inputType == typeof(Document)
                || (inputType.IsGenericType && inputType.GetGenericTypeDefinition() == typeof(ChangeFeedItem<>))
                || inputType == typeof(StoredProcedureResponse)
                || inputType == typeof(TriggerResponse)
                || inputType == typeof(UserDefinedFunctionResponse);

            if (!typeIsWhiteListed && inputIsClientOrDirect)
            {
                throw new ArgumentException($"User serializer is being used for internal type:{inputType.FullName}.");
            }
#endif

            return this.customSerializer;
        }

        private CosmosSerializer GetDefaultSerializer(
            bool canUseBinaryEncodingForPointOperations = false)
        {
            return this.isBinaryEncodingEnabled && canUseBinaryEncodingForPointOperations
                ? CosmosSerializerCore.binarySerializer
                : CosmosSerializerCore.propertiesSerializer;
        }

        internal static bool IsInputTypeInternal(
            Type inputType)
        {
            return inputType == typeof(AccountProperties)
                || inputType == typeof(DatabaseProperties)
                || inputType == typeof(ContainerProperties)
                || inputType == typeof(PermissionProperties)
                || inputType == typeof(StoredProcedureProperties)
                || inputType == typeof(TriggerProperties)
                || inputType == typeof(UserDefinedFunctionProperties)
                || inputType == typeof(UserProperties)
                || inputType == typeof(ConflictProperties)
                || inputType == typeof(ThroughputProperties)
                || inputType == typeof(OfferV2)
                || inputType == typeof(ClientEncryptionKeyProperties)
                || inputType == typeof(PartitionedQueryExecutionInfo)
                || inputType == typeof(ChangeFeedQuerySpec);
        }
    }
}