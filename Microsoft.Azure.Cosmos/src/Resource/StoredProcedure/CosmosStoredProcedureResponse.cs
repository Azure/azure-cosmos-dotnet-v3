//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos stored procedure response
    /// </summary>
    public class CosmosStoredProcedureResponse : CosmosResponse<CosmosStoredProcedureSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosStoredProcedureResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosStoredProcedureResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosStoredProcedureResponse(
           HttpStatusCode httpStatusCode,
           CosmosResponseMessageHeaders headers,
           CosmosStoredProcedureSettings cosmosStoredProcedure) : base(
               httpStatusCode,
               headers,
               cosmosStoredProcedure)
        {
        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// Get <see cref="CosmosStoredProcedureSettings"/> implictly from <see cref="CosmosStoredProcedureResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosStoredProcedureSettings(CosmosStoredProcedureResponse response)
        {
            return response.Resource;
        }
    }
}