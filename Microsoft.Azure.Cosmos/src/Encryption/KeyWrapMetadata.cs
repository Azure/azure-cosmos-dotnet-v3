//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Metadata that a key wrapping provider can use to wrap/unwrap keys.
    /// <seealso cref="KeyWrapProvider" />
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
         class KeyWrapMetadata : IEquatable<KeyWrapMetadata>
    {
        /// <summary>
        /// Creates a new instance of key wrap metadata.
        /// </summary>
        /// <param name="value">Value of the metadata.</param>
        public KeyWrapMetadata(string value)
        {
            this.Type = "custom";
            this.Value = value;
        }

        internal KeyWrapMetadata(KeyWrapMetadata source)
        {
            this.Type = source.Type;
            this.Value = source.Value;
        }

        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        internal string Type { get; set; }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public string Value { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            KeyWrapMetadata metadata = obj as KeyWrapMetadata;
            return this.Equals(metadata);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = 1265339359;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Type);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Value);
            return hashCode;
        }

        /// <summary>
        /// Returns whether the properties of the passed in key wrap metadata matches with those in the current instance.
        /// </summary>
        /// <param name="other">Key wrap metadata to be compared with current instance.</param>
        /// <returns>
        /// True if the properties of the key wrap metadata passed in matches with those in the current instance, else false.
        /// </returns>
        public bool Equals(KeyWrapMetadata other)
        {
            return other != null &&
                   this.Type == other.Type &&
                   this.Value == other.Value;
        }
    }
}