//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    /// <summary>
    /// The cosmos trigger response
    /// </summary>
    internal class TriggerResponse : CosmosResponse<CosmosTriggerSettings>
    {
        /// <summary>
        /// Create a <see cref="TriggerResponse"/> as a no-op for mock testing
        /// </summary>
        public TriggerResponse() : base()
        {

        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal TriggerResponse(
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
        /// Get <see cref="CosmosTrigger"/> implicitly from <see cref="TriggerResponse"/>
        /// </summary>
        /// <param name="response">UserDefinedFunctionResponse</param>
        public static implicit operator CosmosTrigger(TriggerResponse response)
        {
            return response.Trigger;
        }
    }
}