//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Security;

    /// <summary>
    /// Cosmos DB Master Key Credential.
    /// </summary>
    public sealed class CosmosMasterKeyCredential : IDisposable
    {
        private readonly ConcurrentBag<IComputeHash> hashFunctionList;

        /// <summary>
        /// Creates a new instance of <see cref="CosmosMasterKeyCredential"/>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint to use</param>
        /// <param name="authKey">Initial Cosmos DB authentication key.</param>
        public CosmosMasterKeyCredential(string accountEndpoint, string authKey)
            : this(accountEndpoint, new StringHMACSHA256Hash(authKey))
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="CosmosMasterKeyCredential"/>.
        /// </summary>
        /// <param name="accountEndpoint">The cosmos service endpoint.</param>
        /// <param name="authKey">Initial Cosmos DB authentication key.</param>
        public CosmosMasterKeyCredential(string accountEndpoint, SecureString authKey)
            : this(accountEndpoint, new SecureStringHMACSHA256Helper(authKey))
        {
        }

        private CosmosMasterKeyCredential(string accountEndpoint, IComputeHash hashFunction)
        {
            if (string.IsNullOrEmpty(accountEndpoint))
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            this.Endpoint = new Uri(accountEndpoint);
            this.hashFunctionList = new ConcurrentBag<IComputeHash>();

            this.SetHashFunction(hashFunction);
        }

        /// <summary>
        /// Gets the cosmos service endpoint.
        /// </summary>
        public Uri Endpoint { get; }

        internal IComputeHash HashFunction { get; private set; }

        internal int HashFunctionCount => this.hashFunctionList.Count;

        /// <summary>
        /// Updates the authentication key representing this master key credential.
        /// </summary>
        /// <param name="authKey">Cosmos DB authentication key.</param>
        public void UpdateKey(string authKey)
        {
            this.SetHashFunction(new StringHMACSHA256Hash(authKey));
        }

        /// <summary>
        /// Updates the authentication key representing this master key credential.
        /// </summary>
        /// <param name="authKey">Cosmos DB authentication key.</param>
        public void UpdateKey(SecureString authKey)
        {
            this.SetHashFunction(new SecureStringHMACSHA256Helper(authKey));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            while (this.hashFunctionList.TryTake(out IComputeHash hashFunction))
            {
                hashFunction.Dispose();
            }
        }

        private void SetHashFunction(IComputeHash hashFunction)
        {
            this.HashFunction = hashFunction;
            this.hashFunctionList.Add(hashFunction);
        }
    }
}
