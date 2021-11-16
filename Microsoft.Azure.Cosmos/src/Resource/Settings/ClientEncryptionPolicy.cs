//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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
            ClientEncryptionPolicy.ValidateIncludedPaths(includedPaths);
            this.IncludedPaths = includedPaths;
            this.PolicyFormatVersion = 1;
        }

        [JsonConstructor]
        private ClientEncryptionPolicy()
        {
        }

        /// <summary>
        /// Paths of the item that need encryption along with path-specific settings.
        /// </summary>
        [JsonProperty(PropertyName = "includedPaths")]
        public IEnumerable<ClientEncryptionIncludedPath> IncludedPaths
        {
            get; private set;
        } 

        /// <summary>
        /// Version of the client encryption policy definition.
        /// </summary>
        [JsonProperty(PropertyName = "policyFormatVersion")]
        public int PolicyFormatVersion { get; private set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        /// <summary>
        /// Ensures that partition key paths are not specified in the client encryption policy for encryption.
        /// </summary>
        /// <param name="partitionKeyPathTokens">Tokens corresponding to validated partition key.</param>
        internal void ValidatePartitionKeyPathsAreNotEncrypted(IReadOnlyList<IReadOnlyList<string>> partitionKeyPathTokens)
        {
            Debug.Assert(partitionKeyPathTokens != null);
            IEnumerable<string> propertiesToEncrypt = this.IncludedPaths.Select(p => p.Path.Substring(1));
            foreach (IReadOnlyList<string> tokensInPath in partitionKeyPathTokens)
            {
                Debug.Assert(tokensInPath != null);
                if (tokensInPath.Count > 0)
                {
                    string topLevelToken = tokensInPath.First();
                    if (propertiesToEncrypt.Contains(topLevelToken))
                    {
                        throw new ArgumentException($"Paths which are part of the partition key may not be included in the {nameof(ClientEncryptionPolicy)}.", nameof(ContainerProperties.ClientEncryptionPolicy));
                    }
                }
            }
        }

        private static void ValidateIncludedPaths(IEnumerable<ClientEncryptionIncludedPath> clientEncryptionIncludedPath)
        {
            List<string> includedPathsList = new List<string>();
            foreach (ClientEncryptionIncludedPath path in clientEncryptionIncludedPath)
            {
                ClientEncryptionPolicy.ValidateClientEncryptionIncludedPath(path);
                if (includedPathsList.Contains(path.Path))
                {
                    throw new ArgumentException("Duplicate Path found.", nameof(clientEncryptionIncludedPath));
                }

                includedPathsList.Add(path.Path);
            }
        }

        private static void ValidateClientEncryptionIncludedPath(ClientEncryptionIncludedPath clientEncryptionIncludedPath)
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
                !string.Equals(clientEncryptionIncludedPath.EncryptionType, "Randomized"))
            {
                throw new ArgumentException("EncryptionType should be either 'Deterministic' or 'Randomized'. ", nameof(clientEncryptionIncludedPath));
            }

            if (string.IsNullOrWhiteSpace(clientEncryptionIncludedPath.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(clientEncryptionIncludedPath.EncryptionAlgorithm));
            }

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionAlgorithm, "AEAD_AES_256_CBC_HMAC_SHA256"))
            {
                throw new ArgumentException("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'. ", nameof(clientEncryptionIncludedPath));
            }
        }
    }
}