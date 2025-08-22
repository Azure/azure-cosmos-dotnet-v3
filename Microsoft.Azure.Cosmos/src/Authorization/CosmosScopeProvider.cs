//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Authorization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using global::Azure.Core;

    internal sealed class CosmosScopeProvider : IScopeProvider
    {
        private const string AadInvalidScopeErrorMessage = "AADSTS500011";
        private const string AadDefaultScope = "https://cosmos.azure.com/.default";
        private const string ScopeFormat = "https://{0}/.default";

        private readonly string accountScope;
        private readonly string overrideScope;
        private string currentScope;

        public CosmosScopeProvider(Uri accountEndpoint)
        {
            this.overrideScope = ConfigurationManager.AADScopeOverrideValue(defaultValue: null);
            this.accountScope = string.Format(ScopeFormat, accountEndpoint.Host);
            this.currentScope = this.overrideScope ?? this.accountScope;
        }

        public TokenRequestContext GetTokenRequestContext()
        {
            return new TokenRequestContext(new[] { this.currentScope });
        }

        public bool TryFallback(Exception exception)
        {
            // If override scope is set, never fallback
            if (!string.IsNullOrEmpty(this.overrideScope))
            {
                return false;
            }

            // If already using fallback scope, do not fallback again
            if (this.currentScope == CosmosScopeProvider.AadDefaultScope)
            {
                return false;
            }

#pragma warning disable CDX1003 // DontUseExceptionToString
            if (exception.ToString().Contains(CosmosScopeProvider.AadInvalidScopeErrorMessage) == true)
            {
                this.currentScope = CosmosScopeProvider.AadDefaultScope;
                return true;
            }
#pragma warning restore CDX1003 // DontUseExceptionToString

            return false;
        }

        public void Dispose()
        { 
        }
    }
}
