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
    }
}
