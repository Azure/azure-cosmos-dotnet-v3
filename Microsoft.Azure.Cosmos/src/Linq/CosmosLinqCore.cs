//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Helper class to invoke User Defined Functions via Linq queries in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class CosmosLinqCore : CosmosLinq
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public override object InvokeUserDefinedFunction(string udfName, params object[] arguments)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ClientResources.InvalidCallToUserDefinedFunctionProvider));
        }
    }
}
