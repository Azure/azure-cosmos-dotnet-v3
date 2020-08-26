//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class KeyVaultConstants
    {
        internal const string KeysSegment = "keys/";
        internal const string AuthenticationChallengePrefix = "Bearer ";
        internal const string AuthenticationResponseHeaderName = "WWW-Authenticate";
        internal const string AuthenticationParameter = "authorization";

        internal const string RsaOaep256 = "RSA-OAEP-256";

        internal static class DeletionRecoveryLevel
        {
            public const string Purgeable = "Purgeable";
            public const string Recoverable = "Recoverable";
            public const string RecoverableProtectedSubscription = "Recoverable+ProtectedSubscription";
            public const string RecoverablePurgeable = "Recoverable+Purgeable";
            public const string CustomizedRecoverable = "CustomizedRecoverable";
            public const string CustomizedRecoverableProtectedSubscription = "CustomizedRecoverable+ProtectedSubscription";
            public const string CustomizedRecoverablePurgeable = "CustomizedRecoverable+Purgeable";
        }
    }
}
