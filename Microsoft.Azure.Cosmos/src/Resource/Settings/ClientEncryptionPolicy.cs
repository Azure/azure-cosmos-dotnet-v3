//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
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
        /// Initializes a new instance of the <see cref="ClientEncryptionPolicy"/> class.
        /// </summary>
        /// <param name="includedPaths">List of paths to include in the policy definition.</param>
        public ClientEncryptionPolicy(IEnumerable<ClientEncryptionIncludedPath> includedPaths)
        {
            this.ValidateIncludedPaths(includedPaths);
            this.IncludedPaths = includedPaths;
            this.PolicyFormatVersion = 1;
        }

        /// <summary>
        /// Paths of the item that need encryption along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = "includedPaths")]
        public IEnumerable<ClientEncryptionIncludedPath> IncludedPaths
        {
            get; private set;
        } 

        [JsonProperty(PropertyName = "policyFormatVersion")]
        internal int PolicyFormatVersion { get; set; }

        private void ValidateIncludedPaths(IEnumerable<ClientEncryptionIncludedPath> clientEncryptionIncludedPath)
        {
            List<string> includedPathsList = new List<string>();
            foreach (ClientEncryptionIncludedPath path in clientEncryptionIncludedPath)
            {
                this.ValidateClientEncryptionIncludedPath(path);
                if (includedPathsList.Contains(path.Path))
                {
                    throw new ArgumentException("Duplicate Path found.", nameof(clientEncryptionIncludedPath));
                }

                includedPathsList.Add(path.Path);
            }
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

            if (clientEncryptionIncludedPath.Path[0] != '/'
                || clientEncryptionIncludedPath.Path.LastIndexOf('/') != 0
                || string.Equals(clientEncryptionIncludedPath.Path.Substring(1), "id"))
            {
                throw new ArgumentException($"Invalid path '{clientEncryptionIncludedPath.Path ?? string.Empty}'.");
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
                !string.Equals(clientEncryptionIncludedPath.EncryptionType, "Randomized") &&
                !string.Equals(clientEncryptionIncludedPath.EncryptionType, "Plaintext"))
            {
                throw new ArgumentException("EncryptionType should be either 'Deterministic' or 'Randomized' or 'Plaintext'.", nameof(clientEncryptionIncludedPath));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.EncryptionAlgorithm));
            }

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionAlgorithm, "AEAD_AES_256_CBC_HMAC_SHA256"))
            {
                throw new ArgumentException("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'.", nameof(clientEncryptionIncludedPath));
            }
        }
    }
}