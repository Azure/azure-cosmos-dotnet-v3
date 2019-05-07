//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System.Net;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    public class CosmosTriggerResponse : CosmosResponse<CosmosTriggerSettings>
    {
        /// <summary>
        /// Create a <see cref="CosmosTriggerResponse"/> as a no-op for mock testing
        /// </summary>
        public CosmosTriggerResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal CosmosTriggerResponse(
           HttpStatusCode httpStatusCode,
           CosmosResponseMessageHeaders headers,
           CosmosTriggerSettings cosmosTriggerSettings) : base(
               httpStatusCode,
               headers,
               cosmosTriggerSettings)
        {
        }

        /// <summary>
        /// Get <see cref="CosmosTriggerSettings"/> implictly from <see cref="CosmosTriggerResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosTriggerSettings(CosmosTriggerResponse response)
        {
            return response.Resource;
        }
    }
}