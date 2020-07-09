//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    internal abstract class PatchOperation
    {
        [JsonProperty(PropertyName = PatchConstants.PropertyNames.OperationType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PatchOperationType OperationType { get; }

        [JsonProperty(PropertyName = PatchConstants.PropertyNames.Path)]
        public string Path { get; }

        public PatchOperation(
            PatchOperationType operationType,
            string path)
        {
            this.OperationType = operationType;
            this.Path = string.IsNullOrWhiteSpace(path)
                ? throw new ArgumentNullException(nameof(path))
                : path;
        }

        public virtual string SerializeValueParameter(
            CosmosSerializer cosmosSerializer)
        {
            return null;
        }

        public string SerializeValue<T>(
            CosmosSerializer serializer,
            T value)
        {
            // Use the user serializer so custom conversions are correctly handled
            using (Stream stream = serializer.ToStream(value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }
}
