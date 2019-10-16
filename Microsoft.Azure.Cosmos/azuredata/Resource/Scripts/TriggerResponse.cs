//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    public class TriggerResponse : Response<TriggerProperties>
    {
        private readonly Response rawResponse;

        /// <summary>
        /// Create a <see cref="TriggerResponse"/> as a no-op for mock testing
        /// </summary>
        protected TriggerResponse()
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal TriggerResponse(
            Response response,
            TriggerProperties triggerProperties)
        {
            this.rawResponse = response;
            this.Value = triggerProperties;
        }

        /// <inheritdoc/>
        public override Response GetRawResponse() => this.rawResponse;

        /// <inheritdoc/>
        public override TriggerProperties Value { get; }

        /// <summary>
        /// Get <see cref="TriggerProperties"/> implicitly from <see cref="TriggerResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator TriggerProperties(TriggerResponse response)
        {
            return response.Value;
        }
    }
}