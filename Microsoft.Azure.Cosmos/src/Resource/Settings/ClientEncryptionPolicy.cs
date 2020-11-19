//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;

    /// <summary>
    /// Client encryption policy.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
        sealed class ClientEncryptionPolicy
    {
        /// <summary>
        /// Initializes a new instance of ClientEncryptionPolicy.
        /// </summary>
        public ClientEncryptionPolicy()
        {
            this.PolicyFormatVersion = 1;
        }

        /// <summary>
        /// Paths of the item that need encryption along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = "includedPaths")]
        public Collection<ClientEncryptionIncludedPath> IncludedPaths { get; internal set; } = new Collection<ClientEncryptionIncludedPath>();

        [JsonProperty(PropertyName = "policyFormatVersion")]
        internal int PolicyFormatVersion { get; set; }
    }
}