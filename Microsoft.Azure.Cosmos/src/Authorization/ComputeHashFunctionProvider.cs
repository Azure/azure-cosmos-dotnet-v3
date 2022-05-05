//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Security;
    using global::Azure;

    /// <summary>
    /// Abstraction for the hash function used by <see cref="AuthorizationTokenProviderMasterKey"/>.
    /// </summary>
    internal abstract class ComputeHashFunctionProvider : IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// Gets the hash function to be used for calculating key based hashes.
        /// </summary>
        public abstract IComputeHash HashFunction { get; }

        /// <summary>
        /// Creates a new <see cref="ComputeHashFunctionProvider"/> from the given credential.
        /// </summary>
        /// <param name="credential">Azure key credential for the cosmos account key.</param>
        /// <returns>Hash function provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the credential is <c>null</c>.</exception>
        public static ComputeHashFunctionProvider From(AzureKeyCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            return new CredentialKeyHashFunctionProvider(credential);
        }

        /// <summary>
        /// Creates a new <see cref="ComputeHashFunctionProvider"/> from the account key.
        /// </summary>
        /// <param name="authKey">Cosmos account key.</param>
        /// <returns>Hash function provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="authKey"/> is <c>null</c> or empty.</exception>
        public static ComputeHashFunctionProvider From(string authKey)
        {
            if (string.IsNullOrEmpty(authKey))
            {
                throw new ArgumentNullException(nameof(authKey));
            }

            return new StaticHashFunctionProvider(new StringHMACSHA256Hash(authKey));
        }

        /// <summary>
        /// Creates a new <see cref="ComputeHashFunctionProvider"/> from the account key.
        /// </summary>
        /// <param name="authKey">Cosmos account key.</param>
        /// <returns>Hash function provider.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="authKey"/> is <c>null</c>.</exception>
        public static ComputeHashFunctionProvider From(SecureString authKey)
        {
            if (authKey == null)
            {
                throw new ArgumentNullException(nameof(authKey));
            }

            return new StaticHashFunctionProvider(new SecureStringHMACSHA256Helper(authKey));
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                this.disposedValue = true;
            }
        }

        #region Derived Implementations

        private sealed class StaticHashFunctionProvider : ComputeHashFunctionProvider
        {
            public StaticHashFunctionProvider(IComputeHash hashFunction)
            {
                this.HashFunction = hashFunction;
            }

            public override IComputeHash HashFunction { get; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.HashFunction?.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        private sealed class CredentialKeyHashFunctionProvider : ComputeHashFunctionProvider
        {
            private readonly object keyUpdateLock;
            private readonly AzureKeyCredential credential;
            private readonly List<IComputeHash> hashFunctionList;

            private string currentKey;
            private IComputeHash currentHash;

            public CredentialKeyHashFunctionProvider(AzureKeyCredential credential)
            {
                this.keyUpdateLock = new object();
                this.credential = credential;
                this.hashFunctionList = new List<IComputeHash>();

                this.RefreshHashFunction();
            }

            public override IComputeHash HashFunction
            {
                get
                {
                    // Update the hash function if the credential's key has changed.
                    if (!object.ReferenceEquals(this.currentKey, this.credential.Key))
                    {
                        this.RefreshHashFunction();
                    }

                    return this.currentHash;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (IComputeHash hashFunction in this.hashFunctionList)
                    {
                        hashFunction.Dispose();
                    }
                }

                base.Dispose(disposing);
            }

            /// <summary>
            /// Updates the current hash function if the key was updated in the credential since the last refresh.
            /// </summary>
            private void RefreshHashFunction()
            {
                lock (this.keyUpdateLock)
                {
                    // Check if we still need an update
                    if (object.ReferenceEquals(this.currentKey, this.credential.Key))
                    {
                        return;
                    }

                    // Update the hash function **before** updating the current key since the latter is used to detect the need for a package update in
                    // the HashFunction getter property.
                    this.currentHash = new StringHMACSHA256Hash(this.credential.Key);
                    this.currentKey = this.credential.Key;

                    this.hashFunctionList.Add(this.currentHash);
                }
            }
        }

        #endregion
    }
}
