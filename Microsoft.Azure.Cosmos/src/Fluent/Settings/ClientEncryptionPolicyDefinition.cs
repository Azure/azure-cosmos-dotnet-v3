//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="ClientEncryptionPolicy"/> fluent definition.
    /// </summary>
#if PREVIEW
    public 
#else
    internal
#endif
        sealed class ClientEncryptionPolicyDefinition
    {
        private readonly Collection<ClientEncryptionIncludedPath> clientEncryptionIncludedPaths = new Collection<ClientEncryptionIncludedPath>();
        private readonly ContainerBuilder parent;
        private readonly Action<ClientEncryptionPolicy> attachCallback;
        private readonly List<string> includedPathsList = new List<string>();

        internal ClientEncryptionPolicyDefinition(
            ContainerBuilder parent,
            Action<ClientEncryptionPolicy> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a <see cref="ClientEncryptionIncludedPath"/> to the current <see cref="ClientEncryptionPolicyDefinition"/>.
        /// </summary>
        /// <param name="path">ClientEncryptionIncludedPath to add.</param>
        /// <returns>An instance of the current <see cref="ClientEncryptionPolicyDefinition"/>.</returns>
        public ClientEncryptionPolicyDefinition WithIncludedPath(ClientEncryptionIncludedPath path)
        {
            this.ValidateClientEncryptionIncludedPath(path);
            this.clientEncryptionIncludedPaths.Add(path);
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            this.attachCallback(new ClientEncryptionPolicy(this.clientEncryptionIncludedPaths));
            return this.parent;
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

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionAlgorithm, "AEAD_AES_256_CBC_HMAC_SHA256"))
            {
                throw new ArgumentException("EncryptionAlgorithm should be 'AEAD_AES_256_CBC_HMAC_SHA256'.", nameof(clientEncryptionIncludedPath));
            }

            if (this.includedPathsList.Contains(clientEncryptionIncludedPath.Path))
            {
                throw new ArgumentException("Duplicate Path found.", nameof(clientEncryptionIncludedPath));
            }

            this.includedPathsList.Add(clientEncryptionIncludedPath.Path);
        }
    }
}
