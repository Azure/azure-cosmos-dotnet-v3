//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
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
            this.attachCallback(new ClientEncryptionPolicy()
            {
                IncludedPaths = this.clientEncryptionIncludedPaths
            });

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

            if (!string.Equals(clientEncryptionIncludedPath.EncryptionAlgorithm, "MdeAeadAes256CbcHmac256Randomized"))
            {
                throw new ArgumentException("EncryptionAlgorithm should be 'MdeAeadAes256CbcHmac256Randomized'.", nameof(clientEncryptionIncludedPath));
            }
        }
    }
}
