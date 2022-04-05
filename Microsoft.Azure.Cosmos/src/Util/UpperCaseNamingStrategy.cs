//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using Newtonsoft.Json.Serialization;

    internal class UpperCaseNamingStrategy : NamingStrategy
    {
        protected override string ResolvePropertyName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return name.ToUpper();
            }

            return null;
        }
    }
}
