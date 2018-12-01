//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
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
        private CosmosTriggerResponse(
            CosmosResponseMessage cosmosResponse,
            CosmosTrigger trigger) : base(cosmosResponse)
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

        /// <summary>
        /// Create the cosmos trigger response.
        /// Creates the response object, deserializes the
        /// http content stream, and disposes of the HttpResponseMessage
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="CosmosResponseMessage"/> from the Cosmos DB service</param>
        /// <param name="jsonSerializer">The cosmos json serializer</param>
        /// <param name="trigger">The cosmos trigger</param>
        internal static CosmosTriggerResponse CreateResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer,
            CosmosTrigger trigger)
        {
            return CosmosResponse<CosmosTriggerSettings>
                .InitResponse<CosmosTriggerResponse, CosmosTriggerSettings>(
                    (httpResponse) => new CosmosTriggerResponse(cosmosResponseMessage, trigger),
                    jsonSerializer,
                    cosmosResponseMessage);
        }
    }
}