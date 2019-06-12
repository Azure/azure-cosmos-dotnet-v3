//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    /// <summary>
    /// Extensions to interact with Scripts.
    /// </summary>
    /// <seealso cref="StoredProcedureProperties"/>
    /// <seealso cref="TriggerProperties"/>
    /// <seealso cref="UserDefinedFunctionProperties"/>
    public static class ScriptsExtensions
    {
        /// <summary>
        /// Obtains an accessor to Cosmos Scripts.
        /// </summary>
        /// <param name="container">An existing <see cref="Container"/>.</param>
        /// <returns>An instance of of <see cref="Scripts"/>.</returns>
        public static Scripts GetScripts(this Container container)
        {
            CosmosContainerCore cosmosContainerCore = (CosmosContainerCore)container;
            return new CosmosScriptsCore(cosmosContainerCore, cosmosContainerCore.ClientContext);
        }
    }
}
