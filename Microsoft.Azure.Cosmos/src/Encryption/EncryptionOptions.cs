//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Options around encryption / decryption of data.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        class EncryptionOptions
    {
        private IReadOnlyList<string> pathsToEncrypt;

        private HashSet<string> validatedPathsToEncrypt;

        private List<string[]> pathsToEncryptSegments;

        internal List<string[]> PathsToEncryptSegments
        {
            get
            {
                Debug.Assert(this.validatedPathsToEncrypt != null);
                return this.pathsToEncryptSegments;
            }
        }

        /// <summary>
        /// Identifier of the data encryption key to be used for encrypting the data in the request payload.
        /// The data encryption key must be suitable for use with the <see cref="EncryptionAlgorithm"/> provided.
        /// </summary>
        /// <remarks>
        /// The <see cref="Encryptor"/> configured on the client is used to retrieve the actual data encryption key.
        /// </remarks>
        public string DataEncryptionKeyId { get; set; }

        /// <summary>
        /// Algorithm to be used for encrypting the data in the request payload.
        /// </summary>
        public string EncryptionAlgorithm { get; set; }

        /// <summary>
        /// For the request payload, list of JSON paths to encrypt.
        /// Example of a path specification: /sensitive
        /// </summary>
        /// <remarks> 
        /// Paths that are not found in the item are ignored. 
        /// Paths should not overlap, eg. passing both /a and /a/b is not valid. 
        /// Array index specifications are not honored. 
        /// </remarks> 
        public IReadOnlyList<string> PathsToEncrypt
        {
            get
            {
                return this.pathsToEncrypt;
            }

            set
            {
                this.pathsToEncrypt = value;
                this.validatedPathsToEncrypt = null;
            }
        }

        internal void Validate()
        {
            if (string.IsNullOrEmpty(this.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(this.DataEncryptionKeyId));
            }

            if (string.IsNullOrEmpty(this.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(this.EncryptionAlgorithm));
            }

            this.ValidatePathsToEncrypt();
        }

        private void ValidatePathsToEncrypt()
        {
            if (this.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(this.PathsToEncrypt));
            }

            if (this.validatedPathsToEncrypt != null && this.validatedPathsToEncrypt.SetEquals(this.PathsToEncrypt))
            {
                return;
            }

            foreach (string path in this.PathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/')
                {
                    throw new ArgumentException($"Invalid path provided: {path ?? string.Empty}", nameof(this.PathsToEncrypt));
                }
            }

            List<string> pathsToEncrypt = this.PathsToEncrypt.OrderBy(p => p).ToList();

            for (int index = 1; index < this.PathsToEncrypt.Count; index++)
            {
                // If path (eg. /foo) is a prefix of another path (eg. /foo/bar), /foo/bar is redundant.
                if (pathsToEncrypt[index].StartsWith(pathsToEncrypt[index - 1]) && pathsToEncrypt[index][pathsToEncrypt[index - 1].Length] == '/')
                {
                    throw new ArgumentException($"Redundant path provided: {pathsToEncrypt[index]}", nameof(this.PathsToEncrypt));
                }
            }

            this.pathsToEncryptSegments = new List<string[]>();
            foreach (string pathToEncrypt in this.PathsToEncrypt)
            {
                this.pathsToEncryptSegments.Add(pathToEncrypt.Split('/'));
            }

            this.validatedPathsToEncrypt = new HashSet<string>(this.PathsToEncrypt);
        }
    }
}