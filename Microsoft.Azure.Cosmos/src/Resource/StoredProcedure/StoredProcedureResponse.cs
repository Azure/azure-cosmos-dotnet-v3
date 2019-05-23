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
    public class StoredProcedureResponse : Response<CosmosStoredProcedureSettings>
    {
        /// <summary>
        /// Create a <see cref="StoredProcedureResponse"/> as a no-op for mock testing
        /// </summary>
        public StoredProcedureResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal StoredProcedureResponse(
           HttpStatusCode httpStatusCode,
           CosmosResponseMessageHeaders headers,
           CosmosStoredProcedureSettings cosmosStoredProcedureSettings)
            : base(
               httpStatusCode,
               headers,
               cosmosStoredProcedureSettings)
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
        /// Get <see cref="CosmosStoredProcedureSettings"/> implictly from <see cref="StoredProcedureResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosStoredProcedureSettings(StoredProcedureResponse response)
        {
            return response.Resource;
        }
    }
}