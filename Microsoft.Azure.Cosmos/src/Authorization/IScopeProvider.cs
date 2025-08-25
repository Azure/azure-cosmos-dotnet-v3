//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Authorization
{
    using System;
    using global::Azure.Core;

    internal interface IScopeProvider
    {
        TokenRequestContext GetTokenRequestContext();
        bool TryFallback(Exception ex);
    }
}
