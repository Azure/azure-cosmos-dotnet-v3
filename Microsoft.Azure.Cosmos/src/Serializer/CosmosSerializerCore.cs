//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is an interface to allow a custom serializer to be used by the CosmosClient
    /// </summary>
    internal partial class CosmosSerializerCore
    {
        private static readonly CosmosSerializer propertiesSerializer = new CosmosJsonSerializerWrapper(
            new CosmosJsonDotNetSerializer(
                ConfigurationManager.IsBinaryEncodingEnabled()));

        private readonly CosmosSerializer customSerializer;
#if !COSMOS_GW_AOT
         private readonly CosmosSerializer sqlQuerySpecSerializer;
         private CosmosSerializer patchOperationSerializer;
#endif
        internal CosmosSerializerCore(
            CosmosSerializer customSerializer = null)
        {
            if (customSerializer == null)
            {
                this.customSerializer = null;
                // this would allow us to set the JsonConverter and inturn handle Serialized/Stream Query Parameter Value.
#if !COSMOS_GW_AOT
                this.sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                    cosmosSerializer: null,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                this.patchOperationSerializer = null;
#endif
            }
            else
            {
                this.customSerializer = new CosmosJsonSerializerWrapper(customSerializer);
#if !COSMOS_GW_AOT
                this.sqlQuerySpecSerializer = CosmosSqlQuerySpecJsonConverter.CreateSqlQuerySpecSerializer(
                    cosmosSerializer: this.customSerializer,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                this.patchOperationSerializer = PatchOperationsJsonConverter.CreatePatchOperationsSerializer(
                    cosmosSerializer: this.customSerializer,
                    propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
#endif
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

        internal Stream ToStream<T>(T input, JsonTypeInfo jsonTypeInfo)
        {
            CosmosSerializer serializer = this.GetSerializer<T>();
            return serializer.ToStream<T>(input, jsonTypeInfo);
        }

        internal Stream ToStreamSqlQuerySpec(SqlQuerySpec input, ResourceType resourceType)
        {
#if !COSMOS_GW_AOT
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
#endif

            // Use System.Text.Json to serialize the input to a MemoryStream
            MemoryStream stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                JsonSerializer.Serialize(writer, input, CosmosSerializerContext.Default.SqlQuerySpec);
            }
            stream.Position = 0;
            return stream;
        }

        internal CosmosSerializer GetCustomOrDefaultSerializer()
        {
            if (this.customSerializer != null)
            {
                return this.customSerializer;
            }

            return CosmosSerializerCore.propertiesSerializer;
        }

        private CosmosSerializer GetSerializer<T>()
        {
            Type inputType = typeof(T);
#if !COSMOS_GW_AOT
            if (inputType == typeof(PatchSpec))
            {
                if (this.patchOperationSerializer == null)
                {
                    this.patchOperationSerializer = PatchOperationsJsonConverter.CreatePatchOperationsSerializer(
                        cosmosSerializer: this.customSerializer ?? new CosmosJsonDotNetSerializer(),
                        propertiesSerializer: CosmosSerializerCore.propertiesSerializer);
                }
                return this.patchOperationSerializer;
            }
#endif

            if (this.customSerializer == null)
            {
                return CosmosSerializerCore.propertiesSerializer;
            }

            if (CosmosSerializerCore.IsInputTypeInternal(inputType))
            {
                return CosmosSerializerCore.propertiesSerializer;
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
            bool typeIsWhiteListed = inputType == typeof(Document) || (inputType.IsGenericType && inputType.GetGenericTypeDefinition() == typeof(ChangeFeedItem<>));

            if (!typeIsWhiteListed && inputIsClientOrDirect)
            {
                throw new ArgumentException($"User serializer is being used for internal type:{inputType.FullName}.");
            }
#endif

            return this.customSerializer;
        }

        internal static bool IsInputTypeInternal(
            Type inputType)
        {
            return inputType == typeof(AccountProperties)
                || inputType == typeof(DatabaseProperties)
                || inputType == typeof(ContainerProperties)
                || inputType == typeof(StoredProcedureProperties)
                || inputType == typeof(TriggerProperties)
                || inputType == typeof(UserDefinedFunctionProperties)
                || inputType == typeof(ConflictProperties)
                || inputType == typeof(ThroughputProperties)
                || inputType == typeof(OfferV2)
                || inputType == typeof(ClientEncryptionKeyProperties)
                || inputType == typeof(PartitionedQueryExecutionInfo)
#if !COSMOS_GW_AOT
                || inputType == typeof(PermissionProperties)
                || inputType == typeof(UserProperties)
                || inputType == typeof(ChangeFeedQuerySpec)
#endif
                ;
        }
    }
}
