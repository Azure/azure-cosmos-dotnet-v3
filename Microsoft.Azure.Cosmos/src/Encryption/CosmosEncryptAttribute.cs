//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary> 
    /// Attribute to be added on properties that need to be encrypted.
    /// See https://tbd for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class CosmosEncryptAttribute : Attribute
    {
    }
}