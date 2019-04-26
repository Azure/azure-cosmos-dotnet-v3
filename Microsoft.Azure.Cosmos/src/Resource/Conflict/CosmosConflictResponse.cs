//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos conflict response
    /// </summary>
    public class CosmosConflictResponse : CosmosResponse<CosmosConflictSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosConflictResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosConflictResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosConflictResponse(
          HttpStatusCode httpStatusCode,
          CosmosResponseMessageHeaders headers,
          CosmosConflictSettings cosmosConflictSettings,
          CosmosConflict cosmosConflict) : base(
              httpStatusCode,
              headers,
              cosmosConflictSettings)
        {
            this.Conflict = cosmosConflict;
        }

        /// <summary>
        /// The reference to the cosmos user defined function. 
        /// This allows additional operations for the user defined function
        /// </summary>
        public virtual CosmosConflict Conflict { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosConflict"/> implicitly from <see cref="CosmosConflictResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosConflict(CosmosConflictResponse response)
        {
            return response.Conflict;
        }
    }
}