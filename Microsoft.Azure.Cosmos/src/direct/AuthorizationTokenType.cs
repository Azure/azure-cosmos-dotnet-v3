//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    internal enum AuthorizationTokenType
    {
        Invalid,
        PrimaryMasterKey,
        PrimaryReadonlyMasterKey,
        SecondaryMasterKey,
        SecondaryReadonlyMasterKey,
        SystemReadOnly, 
        SystemReadWrite,
        SystemAll,
        ResourceToken,
        ComputeGatewayKey,
        AadToken,
        CompoundToken,
        SasToken,
    }

    internal static class AuthorizationTokenTypeExtensions
    {
        private static readonly Dictionary<int, string> CodeNameMap = new Dictionary<int, string>();

        static AuthorizationTokenTypeExtensions()
        {
            AuthorizationTokenTypeExtensions.CodeNameMap[default(int)] = string.Empty;
            foreach (AuthorizationTokenType authorizationTokenType in Enum.GetValues(typeof(AuthorizationTokenType)))
            {
                AuthorizationTokenTypeExtensions.CodeNameMap[(int)authorizationTokenType] = authorizationTokenType.ToString();
            }
        }

        public static string ToAuthorizationTokenTypeString(this Documents.AuthorizationTokenType code)
        {
            return AuthorizationTokenTypeExtensions.CodeNameMap.TryGetValue((int)code, out string value) ? value : code.ToString();
        }
    }
}
