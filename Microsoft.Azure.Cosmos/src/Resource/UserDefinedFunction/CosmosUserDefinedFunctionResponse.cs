//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    public class CosmosUserDefinedFunctionResponse : CosmosResponse<CosmosUserDefinedFunctionSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosUserDefinedFunctionResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosUserDefinedFunctionResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosUserDefinedFunctionResponse(
          HttpStatusCode httpStatusCode,
          CosmosResponseMessageHeaders headers,
          CosmosUserDefinedFunctionSettings cosmosUserDefinedFunctionSettings) : base(
              httpStatusCode,
              headers,
              cosmosUserDefinedFunctionSettings)
        {
        }

        /// <summary>
        /// Get <see cref="CosmosUserDefinedFunctionSettings"/> implicitly from <see cref="CosmosUserDefinedFunctionResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosUserDefinedFunctionSettings(CosmosUserDefinedFunctionResponse response)
        {
            return response.Resource;
        }
    }
}