//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::Azure.Core;

    internal interface IScopeProvider
    {
        TokenRequestContext GetTokenRequestContext();
        bool TryFallback(Exception ex);
    }
}
