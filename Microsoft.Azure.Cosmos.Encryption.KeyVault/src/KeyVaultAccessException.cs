//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using System;
    using System.Net;
    using System.Runtime.Serialization;

    [Serializable]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Code guideline", Scope = "type")]
    internal class KeyVaultAccessException : Exception
    {
        public KeyVaultAccessException(
            HttpStatusCode statusCode,
            KeyVaultErrorCode keyVaultErrorCode,
            string errorMessage)
        {
            this.HttpStatusCode = statusCode;
            this.KeyVaultErrorCode = keyVaultErrorCode;
            this.ErrorMessage = errorMessage;
        }

        protected KeyVaultAccessException(SerializationInfo info, StreamingContext context)
        {
        }

        public HttpStatusCode HttpStatusCode { get; }

        public KeyVaultErrorCode KeyVaultErrorCode { get; }

        public string ErrorMessage { get; }

        public override string ToString()
        {
            return $"KeyVaultAccessClient failed with HttpStatusCode {this.HttpStatusCode} and KeyVaultError {this.KeyVaultErrorCode.ToString()}. {this.ErrorMessage}";
        }
    }
}
