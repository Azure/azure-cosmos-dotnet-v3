// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class DocumentServiceLeaseConverter : JsonConverter
    {
        private static readonly string VersionPropertyName = "version";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DocumentServiceLeaseCore)
                || objectType == typeof(DocumentServiceLeaseCoreEpk);
        }

        public override object ReadJson(
           JsonReader reader,
           Type objectType,
           object existingValue,
           JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonReaderException();
            }

            JObject jObject = JObject.Load(reader);

            if (jObject.TryGetValue(DocumentServiceLeaseConverter.VersionPropertyName, out JToken versionJToken))
            {
                DocumentServiceLeaseVersion documentServiceLeaseVersion = (DocumentServiceLeaseVersion)versionJToken.Value<int>();
                serializer.ContractResolver.ResolveContract(typeof(DocumentServiceLeaseCoreEpk)).Converter = null;
                if (documentServiceLeaseVersion == DocumentServiceLeaseVersion.EPKRangeBasedLease)
                {
                    return serializer.Deserialize(jObject.CreateReader(), typeof(DocumentServiceLeaseCoreEpk));
                }
            }

            serializer.ContractResolver.ResolveContract(typeof(DocumentServiceLeaseCore)).Converter = null;
            return serializer.Deserialize(jObject.CreateReader(), typeof(DocumentServiceLeaseCore));
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (value is DocumentServiceLeaseCore documentServiceLeaseCore)
            {
                serializer.ContractResolver.ResolveContract(typeof(DocumentServiceLeaseCore)).Converter = null;
                serializer.Serialize(writer, documentServiceLeaseCore);
            }

            if (value is DocumentServiceLeaseCoreEpk documentServiceLeaseCoreEpk)
            {
                serializer.ContractResolver.ResolveContract(typeof(DocumentServiceLeaseCoreEpk)).Converter = null;
                serializer.Serialize(writer, documentServiceLeaseCoreEpk);
            }
        }
    }
}
