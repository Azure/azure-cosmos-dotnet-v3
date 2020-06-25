//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class KeyVaultConstants
    {
        internal const string ApiVersionQueryParameters = "api-version=7.0";
        internal const string CorrelationId = "client-request-id";
        internal const string WrapKeySegment = "/wrapkey";
        internal const string UnwrapKeySegment = "/unwrapkey";
        internal const string KeysSegment = "keys/";
        internal const string Bearer = "Bearer";

        internal const string RsaOaep256 = "RSA-OAEP-256";

        internal const int DefaultHttpClientTimeoutInSeconds = 30;
        internal const int DefaultAadRetryCount = 3;
        internal const int DefaultAadRetryIntervalInSeconds = 1;

        internal static class DeletionRecoveryLevel
        {
            public const string Purgeable = "Purgeable";
            public const string Recoverable = "Recoverable";
            public const string RecoverableProtectedSubscription = "Recoverable+ProtectedSubscription";
            public const string RecoverablePurgeable = "Recoverable+Purgeable";
        }
    }
}
