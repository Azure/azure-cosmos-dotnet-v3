//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// Provides a document client extension to augment the named based routing API.
    /// </summary>
    internal static class DocumentClientSwitchLinkExtension
    {
        private static bool configurationUsingSelfLink = true;


        static DocumentClientSwitchLinkExtension()
        {
            DocumentClientSwitchLinkExtension.Reset("Initialization");
        }

        internal static void Reset(string testName)
        {
            // using the randomizer to set the test to use selflink or altlink
            Random random = new Random();
            double randomDouble = random.NextDouble();

            //configurationUsingSelfLink is not threadsafe, so if in testing code has multiple calls from different thread calling DocumentClientSwitchLinkExtension.Reset()
            //at the same time, it will run into problem. But this  won't happen in testing code. Ignore the threading implication here.
            if (randomDouble < 0.5)
            {
                DocumentClientSwitchLinkExtension.configurationUsingSelfLink = true;
            }
            else
            {
                DocumentClientSwitchLinkExtension.configurationUsingSelfLink = false;
            }

            // Uncomment this line to make PCV always using selflink or altlink.
            // DocumentClientSwitchLinkExtension.configurationUsingSelfLink = true;

            Logger.LogLine("Test {0} is using {1}", testName, DocumentClientSwitchLinkExtension.configurationUsingSelfLink ?
                "SelfLink" : "AltLink");
        }

        /// <summary>
        /// Only used in PCV and CTL test, configure to switch between selflink and altlink
        /// </summary>
        /// <returns></returns>
        internal static string GetLink(this CosmosResource resource)
        {
            // testing the altlink ending with "/"
            string altlink = resource.AltLink == null ? null : resource.AltLink + "/";

            if (DocumentClientSwitchLinkExtension.configurationUsingSelfLink)
            {
                return resource.SelfLink ?? altlink;
            }
            else
            {
                return altlink ?? resource.SelfLink;
            }
        }

        internal static string GetIdOrFullName(this CosmosResource resource)
        {
            if (DocumentClientSwitchLinkExtension.configurationUsingSelfLink)
            {
                return resource.ResourceId;
            }
            else
            {
                return resource.AltLink;
            }
        }
        /// <summary>
        /// For Replace operation, swap the link of selflink, 
        /// </summary>
        /// <param name="resource"></param>
        private static void SwapLinkIfNeeded(CosmosResource resource)
        {
            if (!DocumentClientSwitchLinkExtension.configurationUsingSelfLink)
            {
                string temp = resource.SelfLink;
                resource.SelfLink = resource.AltLink;
                resource.AltLink = temp;
            }
        }

        #region Create operation
        /// <summary>
        /// Creates a document as an asychronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner">the document owner.</param>
        /// <param name="document">the document object.</param>
        /// <param name="options">the request options for the request.</param>
        /// <param name="disableAutomaticIdGeneration">Disables the automatic id generation, will throw an exception if id is missing.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Document>> CreateDocumentAsync(this DocumentClient client, CosmosContainerSettings owner, 
                object document, RequestOptions options = null, bool disableAutomaticIdGeneration = false)
        {
            return client.CreateDocumentAsync(owner.GetLink(), document, options, disableAutomaticIdGeneration);
        }


        /// <summary>
        /// Creates a collection as an asychronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner">the document collection owner.</param>
        /// <param name="documentCollection">the Microsoft.Azure.Documents.CosmosContainerSettings.csobject.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosContainerSettings>> CreateDocumentCollectionAsync(this DocumentClient client, CosmosDatabaseSettings owner, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            return client.CreateDocumentCollectionAsync(owner.GetLink(), documentCollection, options);
        }

        /// <summary>
        /// Creates a stored procedure as an asychronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner">the storedprocedure owner.</param>
        /// <param name="storedProcedure">the Microsoft.Azure.Documents.StoredProcedure object.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosStoredProcedureSettings>> CreateStoredProcedureAsync(this DocumentClient client, CosmosContainerSettings owner, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            return client.CreateStoredProcedureAsync(owner.GetLink(), storedProcedure, options);
        }

        /// <summary>
        /// Creates a trigger as an asychronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner">the collection owner.</param>
        /// <param name="trigger">the Microsoft.Azure.Documents.Trigger object.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosTriggerSettings>> CreateTriggerAsync(this DocumentClient client, CosmosContainerSettings owner, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            return client.CreateTriggerAsync(owner.GetLink(), trigger, options);
        }

        /// <summary>
        /// Creates a user defined function as an asychronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner">the collection owner.</param>
        /// <param name="function">the Microsoft.Azure.Documents.UserDefinedFunction object.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> CreateUserDefinedFunctionAsync(this DocumentClient client, CosmosContainerSettings owner, CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            return client.CreateUserDefinedFunctionAsync(owner.GetLink(), function, options);
        }
        #endregion

        #region Delete operation
        /// <summary>
        /// Delete a database as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="database">database.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosDatabaseSettings>> DeleteDatabaseAsync(this DocumentClient client, CosmosDatabaseSettings database, RequestOptions options = null)
        {
            return client.DeleteDatabaseAsync(database.GetLink(), options);
        }

        /// <summary>
        /// Delete a document as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="document"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Document>> DeleteDocumentAsync(this DocumentClient client, Document document, RequestOptions options = null)
        {
            return client.DeleteDocumentAsync(document.GetLink(), options);
        }

        /// <summary>
        /// Delete a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="documentCollection"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosContainerSettings>> DeleteDocumentCollectionAsync(this DocumentClient client, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            return client.DeleteDocumentCollectionAsync(documentCollection.GetLink(), options);
        }

        /// <summary>
        /// Delete a stored procedure as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="storedProcedure"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosStoredProcedureSettings>> DeleteStoredProcedureAsync(this DocumentClient client, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            return client.DeleteStoredProcedureAsync(storedProcedure.GetLink(), options);
        }


        /// <summary>
        /// Delete a trigger as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="trigger"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosTriggerSettings>> DeleteTriggerAsync(this DocumentClient client, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            return client.DeleteTriggerAsync(trigger.GetLink(), options);
        }

        /// <summary>
        /// Delete a user defined function as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="function"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> DeleteUserDefinedFunctionAsync(this DocumentClient client, CosmosUserDefinedFunctionSettings udf, RequestOptions options = null)
        {
            return client.DeleteUserDefinedFunctionAsync(udf.GetLink(), options);
        }

        /// <summary>
        /// Delete a conflict as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="conflict"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Conflict>> DeleteConflictAsync(this DocumentClient client, Conflict conflict, RequestOptions options = null)
        {
            return client.DeleteConflictAsync(conflict.GetLink(), options);
        }
        #endregion

        #region Replace operation
        /// <summary>
        /// Replaces a document as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="documentCollectionUri">the self-link of the document to be updated.</param>
        /// <param name="document">the updated document.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Document>> ReplaceDocumentExAsync(this DocumentClient client, object document, RequestOptions options = null)
        {
            Document typedDocument = Document.FromObject(document);
            if (string.IsNullOrEmpty(typedDocument.Id))
            {
                throw new ArgumentException("If document has to contains the Id proerty");
            }

            SwapLinkIfNeeded(typedDocument);
            return client.ReplaceDocumentAsync(typedDocument, options);
        }


        /// <summary>
        /// Replaces a documentCollection as an asynchronous operation.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="documentCollection"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Task<ResourceResponse<CosmosContainerSettings>> ReplaceDocumentCollectionExAsync(this DocumentClient client, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            SwapLinkIfNeeded(documentCollection);
            return client.ReplaceDocumentCollectionAsync(documentCollection, options);
        }
        
        /// <summary>
        /// Replace the specified stored procedure.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="storedProcedureUri">the self-link for the attachment.</param>
        /// <param name="storedProcedure">the updated stored procedure.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosStoredProcedureSettings>> ReplaceStoredProcedureExAsync(this DocumentClient client, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            SwapLinkIfNeeded(storedProcedure);
            return client.ReplaceStoredProcedureAsync(storedProcedure, options);
        }

        /// <summary>
        /// Replaces a trigger as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="triggerUri">the self-link for the attachment.</param>
        /// <param name="trigger">the updated trigger.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosTriggerSettings>> ReplaceTriggerExAsync(this DocumentClient client, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            SwapLinkIfNeeded(trigger);
            return client.ReplaceTriggerAsync(trigger, options);
        }

        /// <summary>
        /// Replaces a user defined function as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="userDefinedFunctionUri">the self-link for the attachment.</param>
        /// <param name="function">the updated user defined function.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReplaceUserDefinedFunctionExAsync(this DocumentClient client, CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            SwapLinkIfNeeded(function);
            return client.ReplaceUserDefinedFunctionAsync(function, options);
        }

        /// <summary>
        /// Read a database as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="database"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosDatabaseSettings>> ReadDatabaseAsync(this DocumentClient client, CosmosDatabaseSettings database, RequestOptions options = null)
        {
            return client.ReadDatabaseAsync(database.GetLink(), options);
        }

        /// <summary>
        /// Read a document as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="document"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Document>> ReadDocumentAsync(this DocumentClient client, Document document, RequestOptions options = null)
        {
            return client.ReadDocumentAsync(document.GetLink(), options);
        }

        /// <summary>
        /// Read a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="documentCollection"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosContainerSettings>> ReadDocumentCollectionAsync(this DocumentClient client, CosmosContainerSettings documentCollection, RequestOptions options = null)
        {
            return client.ReadDocumentCollectionAsync(documentCollection.GetLink(), options);
        }

        /// <summary>
        /// Read a stored procedure as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="storedProcedure"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosStoredProcedureSettings>> ReadStoredProcedureAsync(this DocumentClient client, CosmosStoredProcedureSettings storedProcedure, RequestOptions options = null)
        {
            return client.ReadStoredProcedureAsync(storedProcedure.GetLink(), options);
        }

        /// <summary>
        /// Read a trigger as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="trigger"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosTriggerSettings>> ReadTriggerAsync(this DocumentClient client, CosmosTriggerSettings trigger, RequestOptions options = null)
        {
            return client.ReadTriggerAsync(trigger.GetLink(), options);
        }

        /// <summary>
        /// Read a user defined function as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="function"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionAsync(this DocumentClient client, CosmosUserDefinedFunctionSettings function, RequestOptions options = null)
        {
            return client.ReadUserDefinedFunctionAsync(function.GetLink(), options);
        }

        /// <summary>
        /// Read a conflict as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="conflict"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<ResourceResponse<Conflict>> ReadConflictAsync(this DocumentClient client, Conflict conflict, RequestOptions options = null)
        {
            return client.ReadConflictAsync(conflict.GetLink(), options);
        }

        /// <summary>
        /// Read a schema as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="schema"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal static Task<ResourceResponse<Schema>> ReadSchemaAsync(this DocumentClient client, Schema schema, RequestOptions options = null)
        {
            return client.ReadSchemaAsync(schema.GetLink(), options);
        }
#endregion

        #region Feed read
        /// <summary>
        /// Reads the feed (sequence) of collections for a database as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<CosmosContainerSettings>> ReadDocumentCollectionFeedAsync(this DocumentClient client, CosmosDatabaseSettings owner, FeedOptions options = null)
        {
            return client.ReadDocumentCollectionFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of stored procedures for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<CosmosStoredProcedureSettings>> ReadStoredProcedureFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadStoredProcedureFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of triggers for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<CosmosTriggerSettings>> ReadTriggerFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadTriggerFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of user defined functions for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<CosmosUserDefinedFunctionSettings>> ReadUserDefinedFunctionFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadUserDefinedFunctionFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of documents for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<dynamic>> ReadDocumentFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadDocumentFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of conflicts for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<FeedResponse<Conflict>> ReadConflictFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadConflictFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Documents.PartitionKeyRange"/> for a database account from the Azure DocumentDB database service as an asynchronous operation.
        /// </summary>
        /// <param name="client">The document client.</param>
        /// <param name="owner">The document collection to read partition key ranger for.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.PartitionKeyRange"/> containing the read resource record.
        /// </returns>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyRange"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.FeedOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.FeedResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public static Task<FeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }

            return client.ReadPartitionKeyRangeFeedAsync(owner.GetLink(), options);
        }

        /// <summary>
        /// Reads the feed (sequence) of schemas for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <param name="owner"></param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal static Task<FeedResponse<Schema>> ReadSchemaFeedAsync(this DocumentClient client, CosmosContainerSettings owner, FeedOptions options = null)
        {
            return client.ReadSchemaFeedAsync(owner.GetLink(), options);
        }

        #endregion Read Feed

        #region Execute

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation.
        /// </summary>
        /// <param name="client">document client.</param>
        /// <typeparam name="TValue">the type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedure">storedProcedure.</param>
        /// <param name="procedureParams">the parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public static Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(this DocumentClient client, CosmosStoredProcedureSettings storedProcedure, params dynamic[] procedureParams)
        {
            return client.ExecuteStoredProcedureAsync<TValue>(storedProcedure.GetLink(), procedureParams);
        }

        public static Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(this DocumentClient client, CosmosStoredProcedureSettings storedProcedure, RequestOptions requestOptions, params dynamic[] procedureParams)
        {
            return client.ExecuteStoredProcedureAsync<TValue>(storedProcedure.GetLink(), requestOptions, procedureParams);
        }

        #endregion Execute

        #region Create Query
        /// <summary>
        /// Extension method to create a query for document collections.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<CosmosContainerSettings> CreateDocumentCollectionQuery(this DocumentClient client, CosmosDatabaseSettings owner,  FeedOptions feedOptions = null)
        {
            return new DocumentQuery<CosmosContainerSettings>(client, ResourceType.Collection, typeof(CosmosDatabaseSettings), owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for document collections.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateDocumentCollectionQuery(this DocumentClient client, CosmosDatabaseSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateDocumentCollectionQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for document collections.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateDocumentCollectionQuery(this DocumentClient client, CosmosDatabaseSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateDocumentCollectionQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<CosmosStoredProcedureSettings> CreateStoredProcedureQuery(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<CosmosStoredProcedureSettings>(client, ResourceType.StoredProcedure, typeof(CosmosContainerSettings), owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateStoredProcedureQuery(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateStoredProcedureQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateStoredProcedureQuery(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateStoredProcedureQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<CosmosTriggerSettings> CreateTriggerQuery(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return client.CreateTriggerQuery(owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateTriggerQuery(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateTriggerQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateTriggerQuery(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateTriggerQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<CosmosUserDefinedFunctionSettings> CreateUserDefinedFunctionQuery(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return client.CreateUserDefinedFunctionQuery(owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateUserDefinedFunctionQuery(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateUserDefinedFunctionQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateUserDefinedFunctionQuery(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateUserDefinedFunctionQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<Conflict> CreateConflictQuery(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return client.CreateConflictQuery(owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateConflictQuery(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateConflictQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateConflictQuery(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateConflictQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<T> CreateDocumentQuery<T>(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return client.CreateDocumentQuery<T>(owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<T> CreateDocumentQuery<T>(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateDocumentQuery<T>(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<T> CreateDocumentQuery<T>(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateDocumentQuery<T>(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IOrderedQueryable<Document> CreateDocumentQuery(this DocumentClient client, CosmosContainerSettings owner, FeedOptions feedOptions = null)
        {
            return client.CreateDocumentQuery(owner.GetLink(), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="sqlExpression">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateDocumentQuery(this DocumentClient client, CosmosContainerSettings owner, string sqlExpression, FeedOptions feedOptions = null)
        {
            return CreateDocumentQuery(client, owner, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents.
        /// </summary>
        /// <param name="client">the DocumentClient to use.</param>
        /// <param name="owner"></param>
        /// <param name="querySpec">the sql query.</param>
        /// <param name="feedOptions">the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IQueryable<dynamic> CreateDocumentQuery(this DocumentClient client, CosmosContainerSettings owner, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return client.CreateDocumentQuery(owner.GetLink(), querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a change feed query for documents.
        /// </summary>
        /// <param name="client">The DocumentClient to use.</param>
        /// <param name="owner">The collection to read documents from.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public static IDocumentQuery<Document> CreateDocumentChangeFeedQuery(this DocumentClient client, CosmosContainerSettings owner, ChangeFeedOptions feedOptions)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            } 

            return client.CreateDocumentChangeFeedQuery(owner.GetLink(), feedOptions);
        }
        #endregion

        #region FeedReader
        /// <summary>
        /// Creates a Feed Reader for Documents.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="documentsFeedOrDatabaseLink">The link for documents or self-link for database in case a partition resolver is used with the client</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <param name="partitionKey">The key used to determine the target collection</param>
        /// <returns>A <see cref="ResourceFeedReader{Document}"/> instance.</returns>
        public static ResourceFeedReader<Document> CreateDocumentFeedReader(this DocumentClient client, CosmosContainerSettings owner,
            FeedOptions options = null, object partitionKey = null)
        {
            return new ResourceFeedReader<Document>(client, ResourceType.Document, options, owner.GetLink(), partitionKey);
        }

        /// <summary>
        /// Creates a Feed Reader for DocumentCollections.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="collectionsLink">The link for collections</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{DocumentCollection}"/> instance.</returns>
        public static ResourceFeedReader<CosmosContainerSettings> CreateDocumentCollectionFeedReader(this DocumentClient client, CosmosDatabaseSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<CosmosContainerSettings>(client, ResourceType.Collection, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for Users.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="usersLink">The link for users</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{User}"/> instance.</returns>
        public static ResourceFeedReader<User> CreateUserFeedReader(this DocumentClient client, CosmosDatabaseSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<User>(client, ResourceType.User, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for Permissions.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="permissionsLink"></param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Permission}"/> instance.</returns>
        public static ResourceFeedReader<Permission> CreatePermissionFeedReader(this DocumentClient client, User user,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Permission>(client, ResourceType.Permission, options, user.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for StoredProcedures.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="storedProceduresLink">The link for stored procedures</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{StoredProcedure}"/> instance.</returns>
        public static ResourceFeedReader<CosmosStoredProcedureSettings> CreateStoredProcedureFeedReader(this DocumentClient client, CosmosContainerSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<CosmosStoredProcedureSettings>(client, ResourceType.StoredProcedure, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for Triggers.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="triggersLink">The link for triggers</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Trigger}"/> instance.</returns>
        public static ResourceFeedReader<CosmosTriggerSettings > CreateTriggerFeedReader(this DocumentClient client, CosmosContainerSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<CosmosTriggerSettings >(client, ResourceType.Trigger, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for UserDefinedFunctions.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="userDefinedFunctionsLink">The link for userDefinedFunctions</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{UserDefinedFunctions}"/> instance.</returns>
        public static ResourceFeedReader<CosmosUserDefinedFunctionSettings> CreateUserDefinedFunctionFeedReader(this DocumentClient client, CosmosContainerSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<CosmosUserDefinedFunctionSettings>(client, ResourceType.UserDefinedFunction, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for Attachments
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="attachmentsLink">The link for attachments</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Database}"/> instance.</returns>
        public static ResourceFeedReader<Attachment> CreateAttachmentFeedReader(this DocumentClient client, Document owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Attachment>(client, ResourceType.Attachment, options, owner.GetLink());
        }

        /// <summary>
        /// Creates a Feed Reader for Conflicts.
        /// </summary>
        /// <param name="client">The <see cref="DocumentClient"/> instance.</param>
        /// <param name="conflictsLink">The link for conflicts</param>
        /// <param name="options">the <see cref="FeedOptions"/> options for the request.</param>
        /// <returns>A <see cref="ResourceFeedReader{Conflict}"/> instance.</returns>
        public static ResourceFeedReader<Conflict> CreateConflictFeedReader(this DocumentClient client, CosmosContainerSettings owner,
            FeedOptions options = null)
        {
            return new ResourceFeedReader<Conflict>(client, ResourceType.Conflict, options, owner.GetLink());
        }
        #endregion
    }
}
