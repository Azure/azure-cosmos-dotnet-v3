//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{

    /// <summary>
    /// The cosmos script type
    /// </summary>
    public enum CosmosScriptType
    {
        StoredProcedure,
        UserDefinedFunction,
        PreTrigger,
        PostTrigger,
    }
}
