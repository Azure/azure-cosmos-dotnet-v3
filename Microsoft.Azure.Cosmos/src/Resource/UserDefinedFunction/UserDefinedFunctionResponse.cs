//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    public class UserDefinedFunctionResponse : Response<CosmosUserDefinedFunctionSettings>
    {
        /// <summary>
        /// Create a <see cref="UserDefinedFunctionResponse"/> as a no-op for mock testing
        /// </summary>
        public UserDefinedFunctionResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal UserDefinedFunctionResponse(
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
        /// <param name="response">UserDefinedFunctionResponse</param>
        public static implicit operator CosmosUserDefinedFunctionSettings(UserDefinedFunctionResponse response)
        {
            return response.Resource;
        }
    }
}