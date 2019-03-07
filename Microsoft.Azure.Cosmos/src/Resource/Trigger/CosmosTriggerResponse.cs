//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    internal class CosmosTriggerResponse : CosmosResponse<CosmosTriggerSettings>
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
           CosmosTriggerSettings cosmosTriggerSettings,
           CosmosTrigger trigger) : base(
               httpStatusCode,
               headers,
               cosmosTriggerSettings)
        {
            this.Trigger = trigger;
        }

        /// <summary>
        /// The reference to the cosmos trigger.
        /// This allows additional operations for the trigger
        /// </summary>
        public virtual CosmosTrigger Trigger { get; private set; }

        /// <summary>
        /// Get <see cref="CosmosTrigger"/> implictly from <see cref="CosmosTriggerResponse"/>
        /// </summary>
        /// <param name="response">CosmosUserDefinedFunctionResponse</param>
        public static implicit operator CosmosTrigger(CosmosTriggerResponse response)
        {
            return response.Trigger;
        }
    }
}