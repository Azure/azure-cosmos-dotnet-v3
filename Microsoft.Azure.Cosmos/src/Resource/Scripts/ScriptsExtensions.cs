//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    /// <summary>
    /// Extensions to interact with Scripts.
    /// </summary>
    /// <seealso cref="StoredProcedureProperties"/>
    /// <seealso cref="CosmosTriggerProperties"/>
    /// <seealso cref="CosmosUserDefinedFunctionProperties"/>
    public static class ScriptsExtensions
    {
        /// <summary>
        /// Obtains an accessor to Cosmos Scripts.
        /// </summary>
        /// <param name="cosmosContainer">An existing <see cref="CosmosContainer"/>.</param>
        /// <returns>An instance of of <see cref="CosmosScripts"/>.</returns>
        public static CosmosScripts GetScripts(this CosmosContainer cosmosContainer)
        {
            CosmosContainerCore cosmosContainerCore = (CosmosContainerCore)cosmosContainer;
            return new CosmosScriptsCore(cosmosContainerCore, cosmosContainerCore.ClientContext);
        }
    }
}
