//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Scripts
{
    /// <summary>
    /// Specifies the type of the trigger in the Azure Cosmos DB service.
    /// </summary> 
    public enum TriggerType : byte
    {
        /// <summary>
        /// Trigger should be executed before the associated operation(s).
        /// </summary>
        Pre = 0x0,

        /// <summary>
        /// Trigger should be executed after the associated operation(s).
        /// </summary>
        Post = 0x1
    }
}
