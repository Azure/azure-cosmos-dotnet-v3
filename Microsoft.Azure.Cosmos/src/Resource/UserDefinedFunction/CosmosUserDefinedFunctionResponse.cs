//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    internal class UserDefinedFunctionResponse : CosmosResponse<CosmosUserDefinedFunctionSettings>
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
        /// Get <see cref="CosmosUserDefinedFunction"/> implicitly from <see cref="UserDefinedFunctionResponse"/>
        /// </summary>
        /// <param name="response">UserDefinedFunctionResponse</param>
        public static implicit operator CosmosUserDefinedFunction(UserDefinedFunctionResponse response)
        {
            return response.UserDefinedFunction;
        }
    }
}