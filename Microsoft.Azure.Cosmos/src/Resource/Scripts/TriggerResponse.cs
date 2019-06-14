//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    public class TriggerResponse : Response<TriggerProperties>
    {
        /// <summary>
        /// Create a <see cref="TriggerResponse"/> as a no-op for mock testing
        /// </summary>
        public TriggerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal TriggerResponse(
           HttpStatusCode httpStatusCode,
           ResponseHeaders headers,
           TriggerProperties triggerProperties)
            : base(
               httpStatusCode,
               headers,
               triggerProperties)
        {
        }

        /// <summary>
        /// Get <see cref="TriggerProperties"/> implictly from <see cref="TriggerResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator TriggerProperties(TriggerResponse response)
        {
            return response.Resource;
        }
    }
}