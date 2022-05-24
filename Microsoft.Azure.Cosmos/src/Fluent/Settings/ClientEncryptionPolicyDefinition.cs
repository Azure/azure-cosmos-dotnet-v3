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
    public sealed class ClientEncryptionPolicyDefinition
    {
        private readonly Collection<ClientEncryptionIncludedPath> clientEncryptionIncludedPaths = new Collection<ClientEncryptionIncludedPath>();
        private readonly ContainerBuilder parent;
        private readonly Action<ClientEncryptionPolicy> attachCallback;
        private readonly int policyFormatVersion;

        internal ClientEncryptionPolicyDefinition(
            ContainerBuilder parent,
            Action<ClientEncryptionPolicy> attachCallback,
            int policyFormatVersion = 1)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
            this.policyFormatVersion = (policyFormatVersion > 2 || policyFormatVersion < 1) ? throw new ArgumentException($"Supported versions of client encryption policy are 1 and 2. ") : policyFormatVersion;
        }

        /// <summary>
        /// Adds a <see cref="ClientEncryptionIncludedPath"/> to the current <see cref="ClientEncryptionPolicyDefinition"/>.
        /// </summary>
        /// <param name="path">ClientEncryptionIncludedPath to add.</param>
        /// <returns>An instance of the current <see cref="ClientEncryptionPolicyDefinition"/>.</returns>
        public ClientEncryptionPolicyDefinition WithIncludedPath(ClientEncryptionIncludedPath path)
        {
            this.clientEncryptionIncludedPaths.Add(path);
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            this.attachCallback(new ClientEncryptionPolicy(this.clientEncryptionIncludedPaths, this.policyFormatVersion));
            return this.parent;
        }
    }
}
