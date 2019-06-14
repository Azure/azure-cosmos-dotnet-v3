//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;

    /// <summary>
    /// The cosmos user defined function response
    /// </summary>
    public class UserDefinedFunctionResponse : Response<UserDefinedFunctionProperties>
    {
        /// <summary>
        /// Create a <see cref="UserDefinedFunctionResponse"/> as a no-op for mock testing
        /// </summary>
        public UserDefinedFunctionResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal UserDefinedFunctionResponse(
          HttpStatusCode httpStatusCode,
          ResponseHeaders headers,
          UserDefinedFunctionProperties userDefinedFunctionProperties)
            : base(
              httpStatusCode,
              headers,
              userDefinedFunctionProperties)
        {
        }

        /// <summary>
        /// Get <see cref="UserDefinedFunctionProperties"/> implicitly from <see cref="UserDefinedFunctionResponse"/>
        /// </summary>
        /// <param name="response">UserDefinedFunctionResponse</param>
        public static implicit operator UserDefinedFunctionProperties(UserDefinedFunctionResponse response)
        {
            return response.Resource;
        }
    }
}