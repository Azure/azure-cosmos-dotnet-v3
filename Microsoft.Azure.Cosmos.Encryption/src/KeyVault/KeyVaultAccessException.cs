//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using global::Azure;

    [Serializable]
    internal class KeyVaultAccessException : RequestFailedException
    {
        public KeyVaultAccessException(HttpStatusCode statusCode, string keyVaultErrorCode, string? errorMessage, Exception? innerException)
            : base((int)statusCode, keyVaultErrorCode, errorMessage, innerException)
        {
        }

        public override string ToString()
        {
            return $"KeyVaultAccessClient failed with HttpStatusCode {this.Status},KeyVaultError {this.ErrorCode}. {this.Message} and Inner Exception:{this.InnerException}";
        }
    }
}
