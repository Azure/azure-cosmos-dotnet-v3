//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// A custom serializer converter for list of Patch operations.>
    /// </summary>
    internal sealed class PatchOperationsJsonConverter : JsonConverter
    {
        private readonly CosmosSerializer userSerializer;

        public PatchOperationsJsonConverter(CosmosSerializer userSerializer)
        {
            this.userSerializer = userSerializer ?? throw new ArgumentNullException(nameof(userSerializer));
        }

        public override bool CanConvert(Type objectType) => true;

        public override bool CanRead => false;

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            IReadOnlyList<PatchOperation> patchOperations = (IReadOnlyList<PatchOperation>)value;

            writer.WriteStartObject();
            writer.WritePropertyName("operations");
            writer.WriteStartArray();

            foreach (PatchOperation operation in patchOperations)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(PatchConstants.PropertyNames.OperationType);
                writer.WriteValue(operation.OperationType.ToEnumMemberString());
                writer.WritePropertyName(PatchConstants.PropertyNames.Path);
                writer.WriteValue(operation.Path);

                if (operation.TrySerializeValueParameter(this.userSerializer, out string valueParam))
                {
                    writer.WritePropertyName(PatchConstants.PropertyNames.Value);
                    writer.WriteRawValue(valueParam);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Only create a custom PatchOperations serializer if there is a customer serializer else
        /// use the default properties serializer
        /// </summary>
        internal static CosmosSerializer CreatePatchOperationsSerializer(
            CosmosSerializer cosmosSerializer,
            CosmosSerializer propertiesSerializer)
        {
            // If both serializers are the same no need for the custom converter
            if (object.ReferenceEquals(cosmosSerializer, propertiesSerializer))
            {
                return propertiesSerializer;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>()
                {
                    new PatchOperationsJsonConverter(cosmosSerializer)
                }
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}