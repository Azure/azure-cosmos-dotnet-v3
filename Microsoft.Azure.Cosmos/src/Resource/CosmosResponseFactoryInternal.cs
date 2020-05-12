//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;

    internal abstract class CosmosResponseFactoryInternal : CosmosResponseFactory
    {
        public abstract FeedResponse<T> CreateChangeFeedUserTypeResponse<T>(
            ResponseMessage responseMessage);

        public abstract FeedResponse<T> CreateChangeFeedUserTypeResponse<T>(
            ResponseMessage responseMessage,
            Documents.ResourceType resourceType);

        public abstract FeedResponse<T> CreateQueryFeedUserTypeResponse<T>(
            ResponseMessage responseMessage);

        public abstract FeedResponse<T> CreateQueryFeedResponse<T>(
            ResponseMessage responseMessage,
            Documents.ResourceType resourceType);

        public abstract ContainerResponse CreateContainerResponse(
            Container container,
            ResponseMessage responseMessage);

        public abstract UserResponse CreateUserResponse(
            User user,
            ResponseMessage responseMessage);

        public abstract PermissionResponse CreatePermissionResponse(
            Permission permission,
            ResponseMessage responseMessage);

        public abstract DatabaseResponse CreateDatabaseResponse(
            Database database,
            ResponseMessage responseMessage);

        public abstract ThroughputResponse CreateThroughputResponse(
            ResponseMessage responseMessage);

        public abstract StoredProcedureResponse CreateStoredProcedureResponse(
            ResponseMessage responseMessage);

        public abstract TriggerResponse CreateTriggerResponse(
            ResponseMessage responseMessage);

        public abstract UserDefinedFunctionResponse CreateUserDefinedFunctionResponse(
            ResponseMessage responseMessage);
    }
}