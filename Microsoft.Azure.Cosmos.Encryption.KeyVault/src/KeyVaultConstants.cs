//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    internal static class KeyVaultConstants
    {
        internal const string ApiVersionQueryParameters = "api-version=7.0";
        internal const string CorrelationId = "client-request-id";
        internal const string WrapKeySegment = "/wrapkey";
        internal const string UnwrapKeySegment = "/unwrapkey";
        internal const string KeysSegment = "keys/";

        internal const string RsaOaep = "RSA-OAEP";

        internal const int DefaultHttpClientTimeoutInSeconds = 30;
        internal const int DefaultAadRetryCount = 3;
        internal const int DefaultAadRetryIntervalInSeconds = 1;
    }
}
