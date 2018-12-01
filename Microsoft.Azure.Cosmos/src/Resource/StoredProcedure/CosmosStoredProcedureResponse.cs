//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

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
        private CosmosStoredProcedureResponse(
            CosmosResponseMessage cosmosResponse,
            CosmosStoredProcedure storedProcedure) : base(cosmosResponse)
        {
            this.StoredProcedure = storedProcedure;
        }

        /// <summary>
        /// The reference to the cosmos stored procedure.
        /// This allows additional operations for the stored procedure
        /// </summary>
        public virtual CosmosStoredProcedure StoredProcedure { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosDatabase"/> implictly from <see cref="CosmosStoredProcedureResponse"/>
        /// </summary>
        /// <param name="response">CosmosStoredProcedureResponse</param>
        public static implicit operator CosmosStoredProcedure(CosmosStoredProcedureResponse response)
        {
            return response.StoredProcedure;
        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// Gets the output from stored procedure console.log() statements.
        /// </summary>
        /// <value>
        /// Output from console.log() statements in a stored procedure.
        /// </value>
        /// <seealso cref="RequestOptions.EnableScriptLogging"/>
        public virtual string ScriptLog
        {
            get
            {
                string logResults = this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.LogResults);
                return string.IsNullOrEmpty(logResults) ? logResults : Uri.UnescapeDataString(logResults);
            }
        }

        /// <summary>
        /// Create the cosmos stored procedure response.
        /// Creates the response object, deserializes the
        /// http content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos json serializer</param>
        /// <param name="storedProcedure">The cosmos stored procedure</param>
        internal static CosmosStoredProcedureResponse CreateResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer,
            CosmosStoredProcedure storedProcedure)
        {
            return CosmosResponse<CosmosStoredProcedureSettings>
                .InitResponse<CosmosStoredProcedureResponse, CosmosStoredProcedureSettings>(
                    (httpResponse) => new CosmosStoredProcedureResponse(httpResponse, storedProcedure),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}