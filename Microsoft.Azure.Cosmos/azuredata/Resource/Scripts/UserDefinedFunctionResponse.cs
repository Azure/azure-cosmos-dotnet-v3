//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    public class UserDefinedFunctionResponse : Response<UserDefinedFunctionProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="UserDefinedFunctionResponse"/> as a no-op for mock testing
        /// </summary>
        protected UserDefinedFunctionResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal UserDefinedFunctionResponse(
            Response response,
            UserDefinedFunctionProperties userDefinedFunctionProperties)
        {
            this.rawResponse = response;
            this.Value = userDefinedFunctionProperties;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override UserDefinedFunctionProperties Value { get; }

        /// <summary>
        /// Get <see cref="UserDefinedFunctionProperties"/> implicitly from <see cref="UserDefinedFunctionResponse"/>
        /// </summary>
        /// <param name="response">UserDefinedFunctionResponse</param>
        public static implicit operator UserDefinedFunctionProperties(UserDefinedFunctionResponse response)
        {
            return response.Value;
        }
    }
}