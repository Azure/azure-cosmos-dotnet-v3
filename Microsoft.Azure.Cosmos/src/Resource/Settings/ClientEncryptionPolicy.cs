//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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

        private Collection<ClientEncryptionIncludedPath> includedPath = new Collection<ClientEncryptionIncludedPath>();

        /// <summary>
        /// Paths of the item that need encryption along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = "includedPaths")]
        public Collection<ClientEncryptionIncludedPath> IncludedPaths { get => this.includedPath; internal set => this.includedPath = this.ValidateIncludedPaths(value); } 

        [JsonProperty(PropertyName = "policyFormatVersion")]
        internal int PolicyFormatVersion { get; set; }

        internal Collection<ClientEncryptionIncludedPath> ValidateIncludedPaths(Collection<ClientEncryptionIncludedPath> clientEncryptionIncludedPath)
        {
            foreach (ClientEncryptionIncludedPath path in clientEncryptionIncludedPath)
            {
                this.ValidateClientEncryptionIncludedPath(path);
            }
            return clientEncryptionIncludedPath;
        }

        private void ValidateClientEncryptionIncludedPath(ClientEncryptionIncludedPath clientEncryptionIncludedPath)
        {
            if (clientEncryptionIncludedPath == null)
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.Path))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.Path));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.ClientEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.ClientEncryptionKeyId));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.EncryptionType))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.EncryptionType));
            }

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionType, "Deterministic") &&
                !string.Equals(clientEncryptionIncludedPath.EncryptionType, "Randomized"))
            {
                throw new ArgumentException("EncryptionType should be either 'Deterministic' or 'Randomized'.", nameof(clientEncryptionIncludedPath));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.EncryptionAlgorithm));
            }

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionAlgorithm, "MdeAeadAes256CbcHmac256"))
            {
                throw new ArgumentException("EncryptionAlgorithm should be 'MdeAeadAes256CbcHmac256'.", nameof(clientEncryptionIncludedPath));
            }
        }
    }
}