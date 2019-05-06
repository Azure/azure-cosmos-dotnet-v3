//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    internal class CosmosUserDefinedFunctionResponse : CosmosResponse<CosmosUserDefinedFunctionSettings>
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
          CosmosUserDefinedFunctionSettings cosmosUserDefinedFunctionSettings,
          CosmosUserDefinedFunction userDefinedFunction) : base(
              httpStatusCode,
              headers,
              cosmosUserDefinedFunctionSettings)
        {
             this.UserDefinedFunction = userDefinedFunction;
        }

        /// <summary>
        /// The reference to the cosmos user defined function. 
        /// This allows additional operations for the user defined function
        /// </summary>
        public virtual CosmosUserDefinedFunction UserDefinedFunction { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosUserDefinedFunction"/> implicitly from <see cref="CosmosUserDefinedFunctionResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosUserDefinedFunction(CosmosUserDefinedFunctionResponse response)
        {
            return response.UserDefinedFunction;
        }
    }
}