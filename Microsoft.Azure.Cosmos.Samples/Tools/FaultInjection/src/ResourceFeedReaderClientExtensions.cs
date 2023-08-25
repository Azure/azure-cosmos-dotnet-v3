//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Client extensions for creating <see cref="ResourceFeedReader{T}"/> objects in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// For additional details and examples, please refer to <see cref="ResourceFeedReader{T}"/>.
    /// </remarks>
    /// <seealso cref="ResourceFeedReader{T}"/>
    /// <seealso cref="Resource"/>
    /// <seealso cref="DocumentClient"/>
    internal static class ResourceFeedReaderClientExtensions
    {
        /// <summary>
        /// Creates a Feed Reader for databases in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Database}"/> instance.</returns>
        public static ResourceFeedReader<Documents.Database> CreateDatabaseFeedReader(this DocumentClient client, FeedOptions options = null)
        {
            return new ResourceFeedReader<Documents.Database>(client, ResourceType.Database, options, null);
        }

        /// <summary>
        /// Creates a Feed Reader for Documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="documentsFeedOrDatabaseLink">The link for documents or self-link for database in case a partition resolver is used with the client</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <param name="partitionKey">The key used to determine the target collection</param>
        /// <returns>A <see cref="ResourceFeedReader{Document}"/> instance.</returns>
        public static ResourceFeedReader<Document> CreateDocumentFeedReader(this DocumentClient client, string documentsFeedOrDatabaseLink,
            FeedOptions options = null, object partitionKey = null)
        {
            return new ResourceFeedReader<Document>(client, ResourceType.Document, options, documentsFeedOrDatabaseLink, partitionKey);
        }

        /// <summary>
        /// Creates a Feed Reader for PartitionKeyRanges in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="partitionKeyRangesLink">The link for partition key ranges</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{PartitionKeyRange}"/> instance.</returns>
        public static ResourceFeedReader<PartitionKeyRange> CreatePartitionKeyRangeFeedReader(this DocumentClient client, string partitionKeyRangesLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<PartitionKeyRange>(client, ResourceType.PartitionKeyRange, options, partitionKeyRangesLink);
        }

        /// <summary>
        /// Creates a Feed Reader for DocumentCollections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="collectionsLink">The link for collections</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{DocumentCollection}"/> instance.</returns>
        public static ResourceFeedReader<DocumentCollection> CreateDocumentCollectionFeedReader(this DocumentClient client, string collectionsLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<DocumentCollection>(client, ResourceType.Collection, options, collectionsLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Users from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="usersLink">The link for users</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{User}"/> instance.</returns>
        public static ResourceFeedReader<Documents.User> CreateUserFeedReader(this DocumentClient client, string usersLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Documents.User>(client, ResourceType.User, options, usersLink);
        }

        /// <summary>
        /// Creates a Feed Reader for User Defined Types from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="userDefinedTypesLink">The link for user defined types</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{UserDefinedType}"/> instance.</returns>
        public static ResourceFeedReader<UserDefinedType> CreateUserDefinedTypeFeedReader(this DocumentClient client, string userDefinedTypesLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<UserDefinedType>(client, ResourceType.UserDefinedType, options, userDefinedTypesLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Permissions from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="permissionsLink"></param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Permission}"/> instance.</returns>
        public static ResourceFeedReader<Documents.Permission> CreatePermissionFeedReader(this DocumentClient client, string permissionsLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Documents.Permission>(client, ResourceType.Permission, options, permissionsLink);
        }

        /// <summary>
        /// Creates a Feed Reader for StoredProcedures from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="storedProceduresLink">The link for stored procedures</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{StoredProcedure}"/> instance.</returns>
        public static ResourceFeedReader<StoredProcedure> CreateStoredProcedureFeedReader(this DocumentClient client, string storedProceduresLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<StoredProcedure>(client, ResourceType.StoredProcedure, options, storedProceduresLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Triggers from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="triggersLink">The link for triggers</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Trigger}"/> instance.</returns>
        public static ResourceFeedReader<Trigger> CreateTriggerFeedReader(this DocumentClient client, string triggersLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Trigger>(client, ResourceType.Trigger, options, triggersLink);
        }

        /// <summary>
        /// Creates a Feed Reader for UserDefinedFunctions from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="userDefinedFunctionsLink">The link for userDefinedFunctions</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{UserDefinedFunctions}"/> instance.</returns>
        public static ResourceFeedReader<UserDefinedFunction> CreateUserDefinedFunctionFeedReader(this DocumentClient client, string userDefinedFunctionsLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<UserDefinedFunction>(client, ResourceType.UserDefinedFunction, options, userDefinedFunctionsLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Attachments from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="attachmentsLink">The link for attachments</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Database}"/> instance.</returns>
        public static ResourceFeedReader<Attachment> CreateAttachmentFeedReader(this DocumentClient client, string attachmentsLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Attachment>(client, ResourceType.Attachment, options, attachmentsLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Conflicts from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="conflictsLink">The link for conflicts</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Conflict}"/> instance.</returns>
        public static ResourceFeedReader<Conflict> CreateConflictFeedReader(this DocumentClient client, string conflictsLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Conflict>(client, ResourceType.Conflict, options, conflictsLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Schemas from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="schemasLink">The link for schemas</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Schema}"/> instance.</returns>
        internal static ResourceFeedReader<Schema> CreateSchemaFeedReader(this DocumentClient client, string schemasLink,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Schema>(client, ResourceType.Schema, options, schemasLink);
        }

        /// <summary>
        /// Creates a Feed Reader for Offers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Offer}"/> instance.</returns>
        public static ResourceFeedReader<Offer> CreateOfferFeedReader(this DocumentClient client, FeedOptions options = null)
        {
            return new ResourceFeedReader<Offer>(client, ResourceType.Offer, options, null);
        }

        /// <summary>
        /// Creates a Feed Reader for snapshots in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Snapshot}"/> instance.</returns>
        public static ResourceFeedReader<Snapshot> CreateSnapshotFeedReader(this DocumentClient client, FeedOptions options = null)
        {
            return new ResourceFeedReader<Snapshot>(client, ResourceType.Snapshot, options, null);
        }
    }
}
