//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// Metadata that a key wrapping provider can use to wrap/unwrap keys.
    /// <seealso cref="IKeyWrapProvider" />
    /// </summary>
    public abstract class KeyWrapMetadata
    {
        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        internal virtual string Type
        {
            get
            {
                return "custom";
            }
        }

        /// <summary>
        /// Serialized form of metadata.
        /// Note: This value is saved in the Cosmos DB service.
        /// Implementors of derived implementations should ensure that this does not have (private) key material or credential information.
        /// </summary>
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public abstract string Value { get; }
    }
}