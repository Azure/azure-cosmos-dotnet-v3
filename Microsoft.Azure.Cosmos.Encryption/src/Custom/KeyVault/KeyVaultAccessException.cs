//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using global::Azure;

    internal class KeyVaultAccessException : RequestFailedException
    {
        public KeyVaultAccessException(int statusCode, string keyVaultErrorCode, string errorMessage, Exception innerException)
            : base(statusCode, keyVaultErrorCode, errorMessage, innerException)
        {
        }
    }
}
