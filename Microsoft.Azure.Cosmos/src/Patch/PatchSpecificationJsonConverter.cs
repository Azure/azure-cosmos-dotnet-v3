//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// A custom serializer converter for <see cref="PatchSpecification"/>
    /// </summary>
    internal sealed class PatchSpecificationJsonConverter : JsonConverter
    {
        private readonly CosmosSerializer userSerializer;

        internal PatchSpecificationJsonConverter(CosmosSerializer userSerializer)
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
            try
            {
                PatchSpecification patchSpecification = (PatchSpecification)value;

                writer.WriteStartObject();
                writer.WritePropertyName("operations");
                writer.WriteStartArray();

                foreach (PatchOperation operation in patchSpecification.Operations)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(PatchConstants.PropertyNames.OperationType);
                    writer.WriteValue(operation.OperationType.ToString().ToLower());
                    writer.WritePropertyName(PatchConstants.PropertyNames.Path);
                    writer.WriteValue(operation.Path);

                    string valueParam = operation.SerializeValueParameter(this.userSerializer);

                    if (valueParam != null)
                    {
                        writer.WritePropertyName(PatchConstants.PropertyNames.Value);
                        writer.WriteRawValue(valueParam);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException($"Serialization of {nameof(PatchSpecification)} failed.", ex);
            }
        }

        /// <summary>
        /// Only create a custom PatchSpecification serializer if there is a customer serializer else
        /// use the default properties serializer
        /// </summary>
        internal static CosmosSerializer CreatePatchSpecificationSerializer(
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
                    new PatchSpecificationJsonConverter(cosmosSerializer)
                }
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}