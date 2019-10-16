//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos stored procedure response
    /// </summary>
    public class StoredProcedureResponse : Response<StoredProcedureProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="StoredProcedureResponse"/> as a no-op for mock testing
        /// </summary>
        protected StoredProcedureResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal StoredProcedureResponse(
            Response response,
            StoredProcedureProperties storedProcedureProperties)
        {
            this.rawResponse = response;
            this.Value = storedProcedureProperties;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override StoredProcedureProperties Value { get; }

        /// <summary>
        /// Get <see cref="StoredProcedureProperties"/> implicitly from <see cref="StoredProcedureResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator StoredProcedureProperties(StoredProcedureResponse response)
        {
            return response.Value;
        }
    }
}