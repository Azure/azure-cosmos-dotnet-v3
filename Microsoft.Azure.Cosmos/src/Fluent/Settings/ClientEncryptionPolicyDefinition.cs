//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="ClientEncryptionPolicy"/> fluent definition.
    /// The <see cref="ClientEncryptionPolicy"/> should be initialized with
    /// policyFormatVersion 2 and "Deterministic" encryption type, if "id" property or properties which are part of partition key need to be encrypted.
    /// All partition key property values included as part of <see cref="ClientEncryptionIncludedPath"/> have to be JSON strings.
    /// </summary>
    /// <example>
    /// This example shows how to create a <see cref="ClientEncryptionPolicy"/> using <see cref="ClientEncryptionPolicyDefinition"/>.
    /// <code language="c#">
    /// <![CDATA[
    /// ClientEncryptionIncludedPath path1 = new ClientEncryptionIncludedPath()
    /// {
    ///     Path = partitionKeyPath,
    ///     ClientEncryptionKeyId = "key1",
    ///     EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
    ///     EncryptionType = "Deterministic"
    /// };
    /// 
    /// ClientEncryptionIncludedPath path2 = new ClientEncryptionIncludedPath()
    /// {
    ///     Path = "/id",
    ///     ClientEncryptionKeyId = "key2",
    ///     EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256",
    ///     EncryptionType = "Deterministic"
    /// };
    /// 
    /// ContainerResponse containerResponse = await this.database.DefineContainer(containerName, partitionKeyPath)
    ///    .WithClientEncryptionPolicy(policyFormatVersion:2)
    ///    .WithIncludedPath(path1)
    ///    .WithIncludedPath(path2)
    ///    .Attach()
    ///    .CreateAsync()
    /// };
    /// ]]>
    /// </code>
    /// </example>
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
