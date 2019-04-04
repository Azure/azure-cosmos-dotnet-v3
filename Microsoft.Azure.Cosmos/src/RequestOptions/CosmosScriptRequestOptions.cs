//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{

    /// <summary>
    /// The cosmos script request options
    /// </summary>
    public class CosmosScriptRequestOptions : CosmosRequestOptions
    {
        internal CosmosScriptType CosmosScriptType { get; set; }
    }
}
