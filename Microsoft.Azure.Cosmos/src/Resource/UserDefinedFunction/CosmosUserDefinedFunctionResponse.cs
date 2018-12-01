//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
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
        private CosmosUserDefinedFunctionResponse(
            CosmosResponseMessage cosmosResponse,
            CosmosUserDefinedFunction userDefinedFunction): base(cosmosResponse)
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

        /// <summary>
        /// Create the cosmos user defined function response.
        /// Creates the response object, deserializes the
        /// HTTP content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos json serializer</param>
        /// <param name="userDefinedFunction">The cosmos user defined function</param>
        internal static CosmosUserDefinedFunctionResponse CreateResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer,
            CosmosUserDefinedFunction userDefinedFunction)
        {
            return CosmosResponse<CosmosUserDefinedFunctionSettings>
                .InitResponse<CosmosUserDefinedFunctionResponse, CosmosUserDefinedFunctionSettings>(
                    (httpResponse) => new CosmosUserDefinedFunctionResponse(cosmosResponseMessage, userDefinedFunction),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}