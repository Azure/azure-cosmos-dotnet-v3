//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Newtonsoft.Json;

    internal class EncryptionProperties
    {
        // System.Text.Json is strict about type coercion by default whereas Newtonsoft.Json
        // happily coerces JSON-numeric-strings (e.g. "3") to int via JsonSerializer. To keep
        // the two adapters byte-for-byte equivalent on the wire formats they accept, the STJ
        // deserializer is asked to allow the same numeric-string coercion the Newtonsoft
        // deserializer already does. This attribute has no effect on Newtonsoft. It is only
        // emitted on net8.0 where the underlying System.Text.Json version exposes it; on
        // netstandard2.0 the streaming MDE adapter is not compiled, so the only consumer of
        // the STJ deserializer there is internal and never observes the divergence.
        [JsonProperty(PropertyName = Constants.EncryptionFormatVersion)]
        [JsonPropertyName(Constants.EncryptionFormatVersion)]
#if NET8_0_OR_GREATER
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
#endif
        public int EncryptionFormatVersion { get; }

        [JsonProperty(PropertyName = Constants.EncryptionDekId)]
        [JsonPropertyName(Constants.EncryptionDekId)]
        public string DataEncryptionKeyId { get; }

        [JsonProperty(PropertyName = Constants.EncryptionAlgorithm)]
        [JsonPropertyName(Constants.EncryptionAlgorithm)]
        public string EncryptionAlgorithm { get; }

        [JsonProperty(PropertyName = Constants.EncryptedData)]
        [JsonPropertyName(Constants.EncryptedData)]
        public byte[] EncryptedData { get; }

        [JsonProperty(PropertyName = Constants.EncryptedPaths)]
        [JsonPropertyName(Constants.EncryptedPaths)]
        public IEnumerable<string> EncryptedPaths { get; }

        public EncryptionProperties(
            int encryptionFormatVersion,
            string encryptionAlgorithm,
            string dataEncryptionKeyId,
            byte[] encryptedData,
            IEnumerable<string> encryptedPaths)
        {
            this.EncryptionFormatVersion = encryptionFormatVersion;
            this.EncryptionAlgorithm = encryptionAlgorithm;
            this.DataEncryptionKeyId = dataEncryptionKeyId;
            this.EncryptedData = encryptedData;
            this.EncryptedPaths = encryptedPaths;
        }
    }
}
