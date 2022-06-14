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
    /// The <see cref="ClientEncryptionPolicy"/> should be initialized with
    /// policyFormatVersion 2 and "Deterministic" encryption type, if "id" property or properties which are part of partition key need to be encrypted.
    /// All partition key property values have to be JSON strings.
    /// </summary>
    /// <example>
    /// This example shows how to create a <see cref="ClientEncryptionPolicy"/>.
    /// <code language="c#">
    /// <![CDATA[
    /// Collection<ClientEncryptionIncludedPath> paths = new Collection<ClientEncryptionIncludedPath>()
    /// {
    ///    new ClientEncryptionIncludedPath()
    ///    {
    ///        Path = partitionKeyPath,
    ///        ClientEncryptionKeyId = "key1",
    ///        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
    ///        EncryptionType = "Deterministic"
    ///    },
    ///    new ClientEncryptionIncludedPath()
    ///    {
    ///        Path = "/id",
    ///        ClientEncryptionKeyId = "key2",
    ///        EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
    ///        EncryptionType = "Deterministic"
    ///    },
    /// };
    /// 
    /// ContainerProperties setting = new ContainerProperties()
    /// {
    ///    Id = containerName,
    ///    PartitionKeyPath = partitionKeyPath,
    ///    ClientEncryptionPolicy = new ClientEncryptionPolicy(includedPaths:paths, policyFormatVersion:2)
    /// };
    /// ]]>
    /// </code>
    /// </example>
    public sealed class ClientEncryptionPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientEncryptionPolicy"/> class.
        /// The <see cref="PolicyFormatVersion"/> will be set to 1.
        /// Note: If you need to include partition key or id field paths as part of <see cref="ClientEncryptionPolicy"/>, please set <see cref="PolicyFormatVersion"/> to 2.
        /// </summary>
        /// <param name="includedPaths">List of paths to include in the policy definition.</param>        
        public ClientEncryptionPolicy(IEnumerable<ClientEncryptionIncludedPath> includedPaths)
        {
            this.PolicyFormatVersion = 1;
            ClientEncryptionPolicy.ValidateIncludedPaths(includedPaths, this.PolicyFormatVersion);
            this.IncludedPaths = includedPaths;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientEncryptionPolicy"/> class.
        /// Note: If you need to include partition key or id field paths as part of <see cref="ClientEncryptionPolicy"/>, please set <see cref="PolicyFormatVersion"/> to 2.
        /// </summary>
        /// <param name="includedPaths">List of paths to include in the policy definition.</param>
        /// <param name="policyFormatVersion"> Version of the client encryption policy definition. Current supported versions are 1 and 2.</param>
        public ClientEncryptionPolicy(IEnumerable<ClientEncryptionIncludedPath> includedPaths, int policyFormatVersion)
        {
            this.PolicyFormatVersion = (policyFormatVersion > 2 || policyFormatVersion < 1) ? throw new ArgumentException($"Supported versions of client encryption policy are 1 and 2. ") : policyFormatVersion;
            ClientEncryptionPolicy.ValidateIncludedPaths(includedPaths, policyFormatVersion);
            this.IncludedPaths = includedPaths;
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
        /// Ensures that partition key paths specified in the client encryption policy for encryption are encrypted using Deterministic encryption algorithm.
        /// </summary>
        /// <param name="partitionKeyPathTokens">Tokens corresponding to validated partition key.</param>
        internal void ValidatePartitionKeyPathsIfEncrypted(IReadOnlyList<IReadOnlyList<string>> partitionKeyPathTokens)
        {
            Debug.Assert(partitionKeyPathTokens != null);

            foreach (IReadOnlyList<string> tokensInPath in partitionKeyPathTokens)
            {
                Debug.Assert(tokensInPath != null);
                if (tokensInPath.Count > 0)
                {
                    string topLevelToken = tokensInPath.First();

                    // paths in included paths start with "/". Get the ClientEncryptionIncludedPath and validate.
                    IEnumerable<ClientEncryptionIncludedPath> encryptedPartitionKeyPath = this.IncludedPaths.Where(p => p.Path.Substring(1).Equals(topLevelToken));
                    
                    if (encryptedPartitionKeyPath.Any())
                    {
                        if (this.PolicyFormatVersion < 2)
                        {
                            throw new ArgumentException($"Path: /{topLevelToken} which is part of the partition key cannot be encrypted with PolicyFormatVersion: {this.PolicyFormatVersion}. Please use PolicyFormatVersion: 2. ");
                        }

                        // for the ClientEncryptionIncludedPath found check the encryption type.
                        if (encryptedPartitionKeyPath.Select(et => et.EncryptionType).FirstOrDefault() != "Deterministic")
                        {
                            throw new ArgumentException($"Path: /{topLevelToken} which is part of the partition key has to be encrypted with Deterministic type Encryption.");
                        }
                    }
                }
            }
        }

        private static void ValidateIncludedPaths(
            IEnumerable<ClientEncryptionIncludedPath> clientEncryptionIncludedPath,
            int policyFormatVersion)
        {
            List<string> includedPathsList = new List<string>();
            foreach (ClientEncryptionIncludedPath path in clientEncryptionIncludedPath)
            {
                ClientEncryptionPolicy.ValidateClientEncryptionIncludedPath(path, policyFormatVersion);
                if (includedPathsList.Contains(path.Path))
                {
                    throw new ArgumentException($"Duplicate Path found: {path.Path}.");
                }

                includedPathsList.Add(path.Path);
            }
        }

        private static void ValidateClientEncryptionIncludedPath(
            ClientEncryptionIncludedPath clientEncryptionIncludedPath,
            int policyFormatVersion)
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
                || clientEncryptionIncludedPath.Path.LastIndexOf('/') != 0)
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

            if (string.Equals(clientEncryptionIncludedPath.Path.Substring(1), "id"))
            {
                if (policyFormatVersion < 2)
                {
                    throw new ArgumentException($"Path: {clientEncryptionIncludedPath.Path} cannot be encrypted with PolicyFormatVersion: {policyFormatVersion}. Please use PolicyFormatVersion: 2. ");
                }

                if (clientEncryptionIncludedPath.EncryptionType != "Deterministic")
                {
                    throw new ArgumentException($"Only Deterministic encryption type is supported for path: {clientEncryptionIncludedPath.Path}. ");
                }
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