//------------------------------------------------------------
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private CosmosJsonSerializer jsonSerializer { get; }

        internal CosmosResponseFactory(CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.jsonSerializer = cosmosJsonSerializer;
        }

        internal CosmosQueryResponse<T> CreateResultSetQueryResponse<T>(
            CosmosResponseMessage cosmosResponseMessage)
        {
            return CosmosDefaultResultSetIterator<T>.CreateCosmosQueryResponse(
                cosmosResponseMessage,
                this.jsonSerializer);
        }

        internal CosmosQueryResponse<T> CreateChangeFeedQueryResponse<T>(
           CosmosResponseMessage cosmosResponseMessage)
        {
            return ChangeFeedResultSetIterator<T>.CreateCosmosQueryFeedResponse<T>(
                cosmosResponseMessage,
                this.jsonSerializer);
        }

        internal CosmosItemResponse<T> CreateItemResponse<T>(
            CosmosResponseMessage cosmosResponseMessage)
        {
            return CosmosItemResponse<T>.CreateResponse<T>(
                cosmosResponseMessage,
                this.jsonSerializer);
        }

        internal CosmosContainerResponse CreateContainerResponse(
            CosmosResponseMessage cosmosResponseMessage, 
            CosmosContainer container)
        {
            return CosmosContainerResponse.CreateResponse(
                cosmosResponseMessage,
                this.jsonSerializer,
                container);
        }

        internal CosmosDatabaseResponse CreateDatabaseResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosDatabase database)
        {
            return CosmosDatabaseResponse.CreateResponse(
                cosmosResponseMessage,
                this.jsonSerializer,
                database);
        }

        internal CosmosStoredProcedureResponse CreateStoredProcedureResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosStoredProcedure storedProcedure)
        {
            return CosmosStoredProcedureResponse.CreateResponse(
                cosmosResponseMessage,
                this.jsonSerializer,
                storedProcedure);
        }

        internal CosmosTriggerResponse CreateTriggerResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosTrigger trigger)
        {
            return CosmosTriggerResponse.CreateResponse(
                cosmosResponseMessage,
                this.jsonSerializer,
                trigger);
        }

        internal CosmosUserDefinedFunctionResponse CreateUserDefinedFunctionResponse(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosUserDefinedFunction userDefinedFunction)
        {
            return CosmosUserDefinedFunctionResponse.CreateResponse(
                cosmosResponseMessage,
                this.jsonSerializer,
                userDefinedFunction);
        }
    }
}