//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Text.Json;
    using Azure.Cosmos.Serialization;

    /// <summary>
    /// The default Cosmos JSON.NET serializer.
    /// </summary>
    internal static class CosmosTextJsonSerializer
    {
        /// <summary>
        /// Creates a Serializer using System.Text.Json for REST type handling.
        /// </summary>
        internal static CosmosSerializer CreatePropertiesSerializer()
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeDataContractConverters(jsonSerializerOptions);
            CosmosTextJsonSerializer.InitializeRESTConverters(jsonSerializerOptions);
            return CosmosSerializer.ForObjectSerializer(new Azure.Core.JsonObjectSerializer(jsonSerializerOptions));
        }

        internal static CosmosSerializer CreateSerializer(JsonSerializerOptions jsonSerializerOptions = null)
        {
            jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeDataContractConverters(jsonSerializerOptions);
            return CosmosSerializer.ForObjectSerializer(new Azure.Core.JsonObjectSerializer(jsonSerializerOptions));
        }

        /// <summary>
        /// System.Text.Json does not support DataContract, so all DataContract types require custom converters, and all Direct classes too.
        /// </summary>
        public static void InitializeDataContractConverters(JsonSerializerOptions serializerOptions)
        {
            serializerOptions.Converters.Add(new TextJsonCosmosSqlQuerySpecConverter());
        }

        public static void InitializeRESTConverters(JsonSerializerOptions serializerOptions)
        {
            serializerOptions.Converters.Add(new TextJsonOfferV2Converter());
            serializerOptions.Converters.Add(new TextJsonAccountConsistencyConverter());
            serializerOptions.Converters.Add(new TextJsonAccountPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonAccountRegionConverter());
            serializerOptions.Converters.Add(new TextJsonCompositePathConverter());
            serializerOptions.Converters.Add(new TextJsonConflictPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonConflictResolutionPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonContainerPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonDatabasePropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonExcludedPathConverter());
            serializerOptions.Converters.Add(new TextJsonIncludedPathConverter());
            serializerOptions.Converters.Add(new TextJsonIndexConverter());
            serializerOptions.Converters.Add(new TextJsonIndexingPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonPermissionPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonSpatialPathConverter());
            serializerOptions.Converters.Add(new TextJsonStoredProcedurePropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonThroughputPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonTriggerPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonUniqueKeyConverter());
            serializerOptions.Converters.Add(new TextJsonUniqueKeyPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonUserDefinedFunctionPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonUserPropertiesConverter());
        }
    }
}
