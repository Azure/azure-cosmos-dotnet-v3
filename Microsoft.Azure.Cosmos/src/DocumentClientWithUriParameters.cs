//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    internal partial class DocumentClient : IDisposable, IAuthorizationTokenProvider
    {
        #region Create operation

        /// <summary>
        /// Creates a document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to create the document in.</param>
        /// <param name="document">the document object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="disableAutomaticIdGeneration">Disables the automatic id generation, will throw an exception if id is missing.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Document>> CreateDocumentAsync(Uri documentCollectionUri, object document, Documents.Client.RequestOptions options = null, bool disableAutomaticIdGeneration = false, CancellationToken cancellationToken = default)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }

            return this.CreateDocumentAsync(documentCollectionUri.OriginalString, document, options, disableAutomaticIdGeneration, cancellationToken);
        }

        /// <summary>
        /// Creates a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to create the collection in.</param>
        /// <param name="documentCollection">the Microsoft.Azure.Documents.DocumentCollection object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionAsync(Uri databaseUri, DocumentCollection documentCollection, Documents.Client.RequestOptions options = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }

            return this.CreateDocumentCollectionAsync(databaseUri.OriginalString, documentCollection, options);
        }

        /// <summary>
        /// Creates(if doesn't exist) or gets(if already exists) a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to create the collection in.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="DocumentCollection"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionIfNotExistsAsync(Uri databaseUri, DocumentCollection documentCollection, Documents.Client.RequestOptions options = null)
        {
            return TaskHelper.InlineIfPossible(() => this.CreateDocumentCollectionIfNotExistsPrivateAsync(databaseUri, documentCollection, options), null);
        }

        private async Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionIfNotExistsPrivateAsync(
            Uri databaseUri, DocumentCollection documentCollection, Documents.Client.RequestOptions options)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }

            if (documentCollection == null)
            {
                throw new ArgumentNullException("documentCollection");
            }

            Uri documentCollectionUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}",
                     databaseUri.OriginalString, Paths.CollectionsPathSegment, Uri.EscapeUriString(documentCollection.Id)), UriKind.Relative);

            try
            {
                return await this.ReadDocumentCollectionAsync(documentCollectionUri, options);
            }
            catch (DocumentClientException dce)
            {
                if (dce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                return await this.CreateDocumentCollectionAsync(databaseUri, documentCollection, options);
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
            }

            return await this.ReadDocumentCollectionAsync(documentCollectionUri, options);
        }

        /// <summary>
        /// Creates a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to create the stored procedure in.</param>
        /// <param name="storedProcedure">the Microsoft.Azure.Documents.StoredProcedure object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<StoredProcedure>> CreateStoredProcedureAsync(Uri documentCollectionUri, StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.CreateStoredProcedureAsync(documentCollectionUri.OriginalString, storedProcedure, options);
        }

        /// <summary>
        /// Creates a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to create the trigger in.</param>
        /// <param name="trigger">the Microsoft.Azure.Documents.Trigger object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Trigger>> CreateTriggerAsync(Uri documentCollectionUri, Trigger trigger, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.CreateTriggerAsync(documentCollectionUri.OriginalString, trigger, options);
        }

        /// <summary>
        /// Creates a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to create the user defined function in.</param>
        /// <param name="function">the Microsoft.Azure.Documents.UserDefinedFunction object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<UserDefinedFunction>> CreateUserDefinedFunctionAsync(Uri documentCollectionUri, UserDefinedFunction function, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.CreateUserDefinedFunctionAsync(documentCollectionUri.OriginalString, function, options);
        }

        /// <summary>
        /// Creates a user defined type as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to create the user defined type in.</param>
        /// <param name="userDefinedType">the Microsoft.Azure.Documents.UserDefinedType object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<ResourceResponse<UserDefinedType>> CreateUserDefinedTypeAsync(Uri databaseUri, UserDefinedType userDefinedType, Documents.Client.RequestOptions options = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.CreateUserDefinedTypeAsync(databaseUri.OriginalString, userDefinedType, options);
        }
        #endregion

        #region Upsert operation

        /// <summary>
        /// Upserts a document as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to upsert the document in.</param>
        /// <param name="document">the document object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="disableAutomaticIdGeneration">Disables the automatic id generation, will throw an exception if id is missing.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Document>> UpsertDocumentAsync(Uri documentCollectionUri, object document, Documents.Client.RequestOptions options = null, bool disableAutomaticIdGeneration = false, CancellationToken cancellationToken = default)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }

            return this.UpsertDocumentAsync(documentCollectionUri.OriginalString, document, options, disableAutomaticIdGeneration, cancellationToken);
        }

        /// <summary>
        /// Upserts a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to upsert the stored procedure in.</param>
        /// <param name="storedProcedure">the Microsoft.Azure.Documents.StoredProcedure object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<StoredProcedure>> UpsertStoredProcedureAsync(Uri documentCollectionUri, StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.UpsertStoredProcedureAsync(documentCollectionUri.OriginalString, storedProcedure, options);
        }

        /// <summary>
        /// Upserts a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to upsert the trigger in.</param>
        /// <param name="trigger">the Microsoft.Azure.Documents.Trigger object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Trigger>> UpsertTriggerAsync(Uri documentCollectionUri, Trigger trigger, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.UpsertTriggerAsync(documentCollectionUri.OriginalString, trigger, options);
        }

        /// <summary>
        /// Upserts a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to upsert the user defined function in.</param>
        /// <param name="function">the Microsoft.Azure.Documents.UserDefinedFunction object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<UserDefinedFunction>> UpsertUserDefinedFunctionAsync(Uri documentCollectionUri, UserDefinedFunction function, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.UpsertUserDefinedFunctionAsync(documentCollectionUri.OriginalString, function, options);
        }

        /// <summary>
        /// Upserts a user defined type as an asynchronous operation  in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to upsert the user defined type in.</param>
        /// <param name="userDefinedType">the Microsoft.Azure.Documents.UserDefinedType object.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<ResourceResponse<UserDefinedType>> UpsertUserDefinedTypeAsync(Uri databaseUri, UserDefinedType userDefinedType, Documents.Client.RequestOptions options = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.UpsertUserDefinedTypeAsync(databaseUri.OriginalString, userDefinedType, options);
        }
        #endregion

        #region Delete operation
        /// <summary>
        /// Delete a database as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Documents.Database>> DeleteDatabaseAsync(Uri databaseUri, Documents.Client.RequestOptions options = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.DeleteDatabaseAsync(databaseUri.OriginalString, options);
        }

        /// <summary>
        /// Delete a document as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">the URI of the document to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Document>> DeleteDocumentAsync(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (documentUri == null)
            {
                throw new ArgumentNullException("documentUri");
            }
            return this.DeleteDocumentAsync(documentUri.OriginalString, options, cancellationToken);
        }

        /// <summary>
        /// Delete a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<DocumentCollection>> DeleteDocumentCollectionAsync(Uri documentCollectionUri, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.DeleteDocumentCollectionAsync(documentCollectionUri.OriginalString, options);
        }

        /// <summary>
        /// Delete a stored procedure as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureUri">the URI of the stored procedure to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<StoredProcedure>> DeleteStoredProcedureAsync(Uri storedProcedureUri, Documents.Client.RequestOptions options = null)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }
            return this.DeleteStoredProcedureAsync(storedProcedureUri.OriginalString, options);
        }

        /// <summary>
        /// Delete a trigger as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerUri">the URI of the trigger to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Trigger>> DeleteTriggerAsync(Uri triggerUri, Documents.Client.RequestOptions options = null)
        {
            if (triggerUri == null)
            {
                throw new ArgumentNullException("triggerUri");
            }
            return this.DeleteTriggerAsync(triggerUri.OriginalString, options);
        }

        /// <summary>
        /// Delete a user defined function as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="functionUri">the URI of the user defined function to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<UserDefinedFunction>> DeleteUserDefinedFunctionAsync(Uri functionUri, Documents.Client.RequestOptions options = null)
        {
            if (functionUri == null)
            {
                throw new ArgumentNullException("functionUri");
            }
            return this.DeleteUserDefinedFunctionAsync(functionUri.OriginalString, options);
        }

        /// <summary>
        /// Delete a conflict as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictUri">the URI of the conflict to delete.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Conflict>> DeleteConflictAsync(Uri conflictUri, Documents.Client.RequestOptions options = null)
        {
            if (conflictUri == null)
            {
                throw new ArgumentNullException("conflictUri");
            }
            return this.DeleteConflictAsync(conflictUri.OriginalString, options);
        }
        #endregion

        #region Replace operation
        /// <summary>
        /// Replaces a document as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">the URI of the document to be updated.</param>
        /// <param name="document">the updated document.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Document>> ReplaceDocumentAsync(Uri documentUri, object document, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (documentUri == null)
            {
                throw new ArgumentNullException("documentUri");
            }
            return this.ReplaceDocumentAsync(documentUri.OriginalString, document, options, cancellationToken);
        }

        /// <summary>
        /// Replaces a document collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">the URI of the document collection to be updated.</param>
        /// <param name="documentCollection">the updated document collection.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<DocumentCollection>> ReplaceDocumentCollectionAsync(Uri documentCollectionUri, DocumentCollection documentCollection, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }

            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceDocumentCollectionPrivateAsync(
                documentCollection,
                options,
                retryPolicyInstance,
                documentCollectionUri.OriginalString), retryPolicyInstance);
        }

        /// <summary>
        /// Replace the specified stored procedure in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureUri">the URI for the stored procedure to be updated.</param>
        /// <param name="storedProcedure">the updated stored procedure.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<StoredProcedure>> ReplaceStoredProcedureAsync(Uri storedProcedureUri, StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }

            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceStoredProcedurePrivateAsync(
                storedProcedure,
                options,
                retryPolicyInstance,
                storedProcedureUri.OriginalString), retryPolicyInstance);
        }

        /// <summary>
        /// Replaces a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerUri">the URI for the trigger to be updated.</param>
        /// <param name="trigger">the updated trigger.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<Trigger>> ReplaceTriggerAsync(Uri triggerUri, Trigger trigger, Documents.Client.RequestOptions options = null)
        {
            if (triggerUri == null)
            {
                throw new ArgumentNullException("triggerUri");
            }

            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceTriggerPrivateAsync(trigger, options, retryPolicyInstance, triggerUri.OriginalString), retryPolicyInstance);
        }

        /// <summary>
        /// Replaces a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionUri">the URI for the user defined function to be updated.</param>
        /// <param name="function">the updated user defined function.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<ResourceResponse<UserDefinedFunction>> ReplaceUserDefinedFunctionAsync(Uri userDefinedFunctionUri, UserDefinedFunction function, Documents.Client.RequestOptions options = null)
        {
            if (userDefinedFunctionUri == null)
            {
                throw new ArgumentNullException("userDefinedFunctionUri");
            }

            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceUserDefinedFunctionPrivateAsync(function, options, retryPolicyInstance, userDefinedFunctionUri.OriginalString), retryPolicyInstance);
        }

        /// <summary>
        /// Replaces a user defined type as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypeUri">the URI for the user defined type to be updated.</param>
        /// <param name="userDefinedType">the updated user defined type.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<ResourceResponse<UserDefinedType>> ReplaceUserDefinedTypeAsync(Uri userDefinedTypeUri, UserDefinedType userDefinedType, Documents.Client.RequestOptions options = null)
        {
            if (userDefinedTypeUri == null)
            {
                throw new ArgumentNullException("userDefinedTypeUri");
            }

            IDocumentClientRetryPolicy retryPolicyInstance = this.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            return TaskHelper.InlineIfPossible(() => this.ReplaceUserDefinedTypePrivateAsync(userDefinedType, options, retryPolicyInstance, userDefinedTypeUri.OriginalString), retryPolicyInstance);
        }
        #endregion

        #region Read operation
        /// <summary>
        /// Reads a <see cref="Database"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">A URI to the Database resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Database"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="databaseUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Database resource where 
        /// // - db_id is the ID property of the Database you wish to read. 
        /// var dbLink = UriFactory.CreateDatabaseUri("db_id");
        /// Database database = await client.ReadDatabaseAsync(dbLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Database"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Documents.Database>> ReadDatabaseAsync(Uri databaseUri, Documents.Client.RequestOptions options = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.ReadDatabaseAsync(databaseUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">A URI to the Document resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when reading a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Document resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection
        /// // - doc_id is the ID property of the Document you wish to read. 
        /// var docUri = UriFactory.CreateDocumentUri("db_id", "coll_id", "doc_id");
        /// Document document = await client.ReadDocumentAsync(docUri);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Document>> ReadDocumentAsync(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (documentUri == null)
            {
                throw new ArgumentNullException("documentUri");
            }
            return this.ReadDocumentAsync(documentUri.OriginalString, options, cancellationToken);
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> as a generic type T from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentUri">A URI to the Document resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.DocumentResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when reading a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Document resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection
        /// // - doc_id is the ID property of the Document you wish to read. 
        /// var docUri = UriFactory.CreateDocumentUri("db_id", "coll_id", "doc_id");
        /// Customer customer = await client.ReadDocumentAsync<Customer>(docUri);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.DocumentResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<DocumentResponse<T>> ReadDocumentAsync<T>(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (documentUri == null)
            {
                throw new ArgumentNullException("documentUri");
            }
            return this.ReadDocumentAsync<T>(documentUri.OriginalString, options, cancellationToken);
        }

        /// <summary>
        /// Reads a <see cref="DocumentCollection"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">A URI to the DocumentCollection resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="DocumentCollection"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentCollectionUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Document resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection you wish to read. 
        /// var collLink = UriFactory.CreateCollectionUri("db_id", "coll_id");
        /// DocumentCollection coll = await client.ReadDocumentCollectionAsync(collLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="DocumentCollection"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<DocumentCollection>> ReadDocumentCollectionAsync(Uri documentCollectionUri, Documents.Client.RequestOptions options = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }
            return this.ReadDocumentCollectionAsync(documentCollectionUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="StoredProcedure"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureUri">A URI to the StoredProcedure resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="StoredProcedure"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a StoredProcedure resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection 
        /// // - sproc_id is the ID property of the StoredProcedure you wish to read. 
        /// var sprocLink = UriFactory.CreateStoredProcedureUri("db_id", "coll_id", "sproc_id");
        /// StoredProcedure sproc = await client.ReadStoredProcedureAsync(sprocLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="StoredProcedure"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<StoredProcedure>> ReadStoredProcedureAsync(Uri storedProcedureUri, Documents.Client.RequestOptions options = null)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }
            return this.ReadStoredProcedureAsync(storedProcedureUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="Trigger"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerUri">A URI to the Trigger resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Trigger"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Trigger resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection 
        /// // - trigger_id is the ID property of the Trigger you wish to read. 
        /// var triggerLink = UriFactory.CreateTriggerUri("db_id", "coll_id", "trigger_id");
        /// Trigger trigger = await client.ReadTriggerAsync(triggerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Trigger"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Trigger>> ReadTriggerAsync(Uri triggerUri, Documents.Client.RequestOptions options = null)
        {
            if (triggerUri == null)
            {
                throw new ArgumentNullException("triggerUri");
            }
            return this.ReadTriggerAsync(triggerUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="UserDefinedFunction"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="functionUri">A URI to the User Defined Function resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="UserDefinedFunction"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="functionUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a UserDefinedFunction resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection 
        /// // - udf_id is the ID property of the UserDefinedFunction you wish to read. 
        /// var udfLink = UriFactory.CreateUserDefinedFunctionUri("db_id", "coll_id", "udf_id");
        /// UserDefinedFunction udf = await client.ReadUserDefinedFunctionAsync(udfLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="UserDefinedFunction"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<UserDefinedFunction>> ReadUserDefinedFunctionAsync(Uri functionUri, Documents.Client.RequestOptions options = null)
        {
            if (functionUri == null)
            {
                throw new ArgumentNullException("functionUri");
            }
            return this.ReadUserDefinedFunctionAsync(functionUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Conflict"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictUri">A URI to the Conflict resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Conflict"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Conflict resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection
        /// // - conflict_id is the ID property of the Conflict you wish to read. 
        /// var conflictLink = UriFactory.CreateConflictUri("db_id", "coll_id", "conflict_id");
        /// Conflict conflict = await client.ReadConflictAsync(conflictLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<ResourceResponse<Conflict>> ReadConflictAsync(Uri conflictUri, Documents.Client.RequestOptions options = null)
        {
            if (conflictUri == null)
            {
                throw new ArgumentNullException("conflictUri");
            }
            return this.ReadConflictAsync(conflictUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Schema"/> as an asynchronous operation.
        /// </summary>
        /// <param name="schemaUri">A URI to the Schema resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Schema"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="schemaUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when reading a Schema are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Document resource where 
        /// // - db_id is the ID property of the Database
        /// // - coll_id is the ID property of the DocumentCollection
        /// // - schema_id is the ID property of the Document you wish to read. 
        /// var docLink = UriFactory.CreateDocumentUri("db_id", "coll_id", "schema_id");
        /// Schema schema = await client.ReadSchemaAsync(schemaLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Schema"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<Schema>> ReadSchemaAsync(Uri schemaUri, Documents.Client.RequestOptions options = null)
        {
            if (schemaUri == null)
            {
                throw new ArgumentNullException("schemaUri");
            }
            return this.ReadSchemaAsync(schemaUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="UserDefinedType"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypeUri">A URI to the UserDefinedType resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="ResourceResponse{T}"/> which wraps a <see cref="UserDefinedType"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedTypeUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a UserDefinedType resource where 
        /// // - db_id is the ID property of the Database
        /// // - userDefinedType_id is the ID property of the UserDefinedType you wish to read. 
        /// var userDefinedTypeLink = UriFactory.CreateUserDefinedTypeUri("db_id", "userDefinedType_id");
        /// UserDefinedType userDefinedType = await client.ReadUserDefinedTypeAsync(userDefinedTypeLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="UserDefinedType"/> 
        /// <seealso cref="RequestOptions"/>
        /// <seealso cref="ResourceResponse{T}"/>
        /// <seealso cref="Task"/>
        internal Task<ResourceResponse<UserDefinedType>> ReadUserDefinedTypeAsync(Uri userDefinedTypeUri, Documents.Client.RequestOptions options = null)
        {
            if (userDefinedTypeUri == null)
            {
                throw new ArgumentNullException("userDefinedTypeUri");
            }
            return this.ReadUserDefinedTypeAsync(userDefinedTypeUri.OriginalString, options);
        }

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Snapshot"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="snapshotUri">A URI to the Snapshot resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Snapshot"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="snapshotUri"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when reading a Snapshot are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Reads a Snapshot resource where 
        /// // - snapshot_id is the ID property of the Snapshot you wish to read. 
        /// var snapshotLink = UriFactory.CreateSnapshotUri("snapshot_id");
        /// Snapshot snapshot = await client.ReadSnapshotAsync(snapshotLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the service. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Snapshot"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        internal Task<ResourceResponse<Snapshot>> ReadSnapshotAsync(Uri snapshotUri, Documents.Client.RequestOptions options = null)
        {
            if (snapshotUri == null)
            {
                throw new ArgumentNullException("snapshotUri");
            }

            return this.ReadSnapshotAsync(snapshotUri.OriginalString, options);
        }

        #endregion

        #region Feed read
        /// <summary>
        /// Reads the feed (sequence) of collections for a database as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionsUri">the URI for the document collections.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<DocumentCollection>> ReadDocumentCollectionFeedAsync(Uri documentCollectionsUri, FeedOptions options = null)
        {
            if (documentCollectionsUri == null)
            {
                throw new ArgumentNullException("documentCollectionsUri");
            }
            return this.ReadDocumentCollectionFeedAsync(documentCollectionsUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of stored procedures for a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProceduresUri">the URI for the stored procedures.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<StoredProcedure>> ReadStoredProcedureFeedAsync(Uri storedProceduresUri, FeedOptions options = null)
        {
            if (storedProceduresUri == null)
            {
                throw new ArgumentNullException("storedProceduresUri");
            }
            return this.ReadStoredProcedureFeedAsync(storedProceduresUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of triggers for a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggersUri">the URI for the triggers.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<Trigger>> ReadTriggerFeedAsync(Uri triggersUri, FeedOptions options = null)
        {
            if (triggersUri == null)
            {
                throw new ArgumentNullException("triggersUri");
            }
            return this.ReadTriggerFeedAsync(triggersUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of user defined functions for a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionsUri">the URI for the user defined functions.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<UserDefinedFunction>> ReadUserDefinedFunctionFeedAsync(Uri userDefinedFunctionsUri, FeedOptions options = null)
        {
            if (userDefinedFunctionsUri == null)
            {
                throw new ArgumentNullException("userDefinedFunctionsUri");
            }
            return this.ReadUserDefinedFunctionFeedAsync(userDefinedFunctionsUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of documents for a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentsUri">the URI for the documents.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<dynamic>> ReadDocumentFeedAsync(Uri documentsUri, FeedOptions options = null, CancellationToken cancellationToken = default)
        {
            if (documentsUri == null)
            {
                throw new ArgumentNullException("documentsUri");
            }
            return this.ReadDocumentFeedAsync(documentsUri.OriginalString, options, cancellationToken);
        }

        /// <summary>
        /// Reads the feed (sequence) of conflicts for a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictsUri">the URI for the conflicts.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<DocumentFeedResponse<Conflict>> ReadConflictFeedAsync(Uri conflictsUri, FeedOptions options = null)
        {
            if (conflictsUri == null)
            {
                throw new ArgumentNullException("conflictsUri");
            }
            return this.ReadConflictFeedAsync(conflictsUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Documents.PartitionKeyRange"/> for a database account from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKeyRangesOrCollectionUri">The Uri for partition key ranges, or owner collection.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.PartitionKeyRange"/> containing the read resource record.
        /// </returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Uri partitionKeyRangesUri = UriFactory.CreatePartitionKeyRangesUri(database.Id, collection.Id);
        /// DoucmentFeedResponse<PartitionKeyRange> response = null;
        /// List<string> ids = new List<string>();
        /// do
        /// {
        ///     response = await client.ReadPartitionKeyRangeFeedAsync(partitionKeyRangesUri, new FeedOptions { MaxItemCount = 1000 });
        ///     foreach (var item in response)
        ///     {
        ///         ids.Add(item.Id);
        ///     }
        /// }
        /// while (!string.IsNullOrEmpty(response.ResponseContinuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyRange"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.FeedOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.DocumentFeedResponse{T}"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.UriFactory.CreatePartitionKeyRangesUri(string, string)"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        public Task<DocumentFeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedAsync(Uri partitionKeyRangesOrCollectionUri, FeedOptions options = null)
        {
            if (partitionKeyRangesOrCollectionUri == null)
            {
                throw new ArgumentNullException("partitionKeyRangesOrCollectionUri");
            }

            return this.ReadPartitionKeyRangeFeedAsync(partitionKeyRangesOrCollectionUri.OriginalString, options);
        }

        /// <summary>
        /// Reads the feed (sequence) of user defined types for a database as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypesUri">the URI for the user defined types.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<DocumentFeedResponse<UserDefinedType>> ReadUserDefinedTypeFeedAsync(Uri userDefinedTypesUri, FeedOptions options = null)
        {
            if (userDefinedTypesUri == null)
            {
                throw new ArgumentNullException("userDefinedTypesUri");
            }
            return this.ReadUserDefinedTypeFeedAsync(userDefinedTypesUri.OriginalString, options);
        }

        #endregion

        #region Execute operation(Stored Procedures)

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">the type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">the URI of the stored procedure to be executed.</param>
        /// <param name="procedureParams">the parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(Uri storedProcedureUri, params dynamic[] procedureParams)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }
            return this.ExecuteStoredProcedureAsync<TValue>(storedProcedureUri.OriginalString, procedureParams);
        }

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">the type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">the URI of the stored procedure to be executed.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="procedureParams">the parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(Uri storedProcedureUri, Documents.Client.RequestOptions options, params dynamic[] procedureParams)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }
            return this.ExecuteStoredProcedureAsync<TValue>(storedProcedureUri.OriginalString, options, procedureParams);
        }

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">the type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">the URI of the stored procedure to be executed.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <param name="procedureParams">the parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        public Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(Uri storedProcedureUri, Documents.Client.RequestOptions options, CancellationToken cancellationToken = default, params dynamic[] procedureParams)
        {
            if (storedProcedureUri == null)
            {
                throw new ArgumentNullException("storedProcedureUri");
            }
            return this.ExecuteStoredProcedureAsync<TValue>(storedProcedureUri.OriginalString, options, cancellationToken, procedureParams);
        }

        /// <summary>
        /// Reads the feed (sequence) of schemas for a collection as an asynchronous operation.
        /// </summary>
        /// <param name="schemasUri">the link for the schemas.</param>
        /// <param name="options">The request options for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        internal Task<DocumentFeedResponse<Schema>> ReadSchemaFeedAsync(Uri schemasUri, FeedOptions options = null)
        {
            if (schemasUri == null)
            {
                throw new ArgumentNullException("schemasUri");
            }
            return this.ReadSchemaFeedAsync(schemasUri.OriginalString, options);
        }
        #endregion

        #region Create Query

        /// <summary>
        /// Extension method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI to the database.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<DocumentCollection> CreateDocumentCollectionQuery(Uri databaseUri, FeedOptions feedOptions = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.CreateDocumentCollectionQuery(databaseUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI to the database.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateDocumentCollectionQuery(Uri databaseUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.CreateDocumentCollectionQuery(databaseUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI to the database.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateDocumentCollectionQuery(Uri databaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException("databaseUri");
            }
            return this.CreateDocumentCollectionQuery(databaseUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for collections under an Azure Cosmos DB database account
        /// in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">Specifies the database to read collections from.</param>
        /// <param name="feedOptions">Specifies the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        internal IDocumentQuery<DocumentCollection> CreateDocumentCollectionChangeFeedQuery(Uri databaseUri, ChangeFeedOptions feedOptions)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException(nameof(databaseUri));
            }

            return this.CreateDocumentCollectionChangeFeedQuery(databaseUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProceduresUri">the URI to the stored procedures.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<StoredProcedure> CreateStoredProcedureQuery(Uri storedProceduresUri, FeedOptions feedOptions = null)
        {
            if (storedProceduresUri == null)
            {
                throw new ArgumentNullException("storedProceduresUri");
            }
            return this.CreateStoredProcedureQuery(storedProceduresUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProceduresUri">the URI to the stored procedures.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateStoredProcedureQuery(Uri storedProceduresUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (storedProceduresUri == null)
            {
                throw new ArgumentNullException("storedProceduresUri");
            }
            return this.CreateStoredProcedureQuery(storedProceduresUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProceduresUri">the URI to the stored procedures.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateStoredProcedureQuery(Uri storedProceduresUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (storedProceduresUri == null)
            {
                throw new ArgumentNullException("storedProceduresUri");
            }
            return this.CreateStoredProcedureQuery(storedProceduresUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggersUri">the URI to the triggers.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<Trigger> CreateTriggerQuery(Uri triggersUri, FeedOptions feedOptions = null)
        {
            if (triggersUri == null)
            {
                throw new ArgumentNullException("triggersUri");
            }
            return this.CreateTriggerQuery(triggersUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggersUri">the URI to the triggers.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateTriggerQuery(Uri triggersUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (triggersUri == null)
            {
                throw new ArgumentNullException("triggersUri");
            }
            return this.CreateTriggerQuery(triggersUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggersUri">the URI to the triggers.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateTriggerQuery(Uri triggersUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (triggersUri == null)
            {
                throw new ArgumentNullException("triggersUri");
            }
            return this.CreateTriggerQuery(triggersUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionsUri">the URI to the user-defined functions.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<UserDefinedFunction> CreateUserDefinedFunctionQuery(Uri userDefinedFunctionsUri, FeedOptions feedOptions = null)
        {
            if (userDefinedFunctionsUri == null)
            {
                throw new ArgumentNullException("userDefinedFunctionsUri");
            }
            return this.CreateUserDefinedFunctionQuery(userDefinedFunctionsUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionsUri">the URI to the user-defined functions.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateUserDefinedFunctionQuery(Uri userDefinedFunctionsUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (userDefinedFunctionsUri == null)
            {
                throw new ArgumentNullException("userDefinedFunctionsUri");
            }
            return this.CreateUserDefinedFunctionQuery(userDefinedFunctionsUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionsUri">the URI to the user-defined functions.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateUserDefinedFunctionQuery(Uri userDefinedFunctionsUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (userDefinedFunctionsUri == null)
            {
                throw new ArgumentNullException("userDefinedFunctionsUri");
            }
            return this.CreateUserDefinedFunctionQuery(userDefinedFunctionsUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictsUri">the URI to the conflicts.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<Conflict> CreateConflictQuery(Uri conflictsUri, FeedOptions feedOptions = null)
        {
            if (conflictsUri == null)
            {
                throw new ArgumentNullException("conflictsUri");
            }
            return this.CreateConflictQuery(conflictsUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictsUri">the URI to the conflicts.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateConflictQuery(Uri conflictsUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (conflictsUri == null)
            {
                throw new ArgumentNullException("conflictsUri");
            }
            return this.CreateConflictQuery(conflictsUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictsUri">the URI to the conflicts.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateConflictQuery(Uri conflictsUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (conflictsUri == null)
            {
                throw new ArgumentNullException("conflictsUri");
            }
            return this.CreateConflictQuery(conflictsUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionUri, FeedOptions feedOptions = null)
        {
            if (documentCollectionUri == null)
            {
                throw new ArgumentNullException("documentCollectionUri");
            }

            return this.CreateDocumentQuery<T>(documentCollectionUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. Please use the override that does not take a partitionKey parameter.")]
        public IOrderedQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionOrDatabaseUri, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }

            return this.CreateDocumentQuery<T>(documentCollectionOrDatabaseUri.OriginalString, feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionOrDatabaseUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }

            return this.CreateDocumentQuery<T>(documentCollectionOrDatabaseUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionOrDatabaseUri, string sqlExpression, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery<T>(documentCollectionOrDatabaseUri, new SqlQuerySpec(sqlExpression), feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionOrDatabaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }

            return this.CreateDocumentQuery<T>(documentCollectionOrDatabaseUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents for the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionOrDatabaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }

            return this.CreateDocumentQuery<T>(documentCollectionOrDatabaseUri.OriginalString, querySpec, feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IOrderedQueryable<Document> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, FeedOptions feedOptions = null)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IOrderedQueryable<Document> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri.OriginalString, feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<dynamic> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, string sqlExpression, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri, new SqlQuerySpec(sqlExpression), feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        public IQueryable<dynamic> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionOrDatabaseUri">The URI of the document collection, e.g. dbs/db_rid/colls/coll_rid/. 
        /// Alternatively, this can be a URI of the database when using an <see cref="T:Microsoft.Azure.Documents.Client.IPartitionResolver"/>, e.g. dbs/db_rid/</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>The query result set.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<dynamic> CreateDocumentQuery(Uri documentCollectionOrDatabaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions, object partitionKey)
        {
            if (documentCollectionOrDatabaseUri == null)
            {
                throw new ArgumentNullException("documentCollectionOrDatabaseUri");
            }
            return this.CreateDocumentQuery(documentCollectionOrDatabaseUri.OriginalString, querySpec, feedOptions, partitionKey);
        }

        /// <summary>
        /// Extension method to create a change feed query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">Specifies the collection to read documents from.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        public IDocumentQuery<Document> CreateDocumentChangeFeedQuery(Uri collectionLink, ChangeFeedOptions feedOptions)
        {
            if (collectionLink == null)
            {
                throw new ArgumentNullException("collectionLink");
            }

            return this.CreateDocumentChangeFeedQuery(collectionLink.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user defined types in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypesUri">the URI to the user defined types.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        internal IOrderedQueryable<UserDefinedType> CreateUserDefinedTypeQuery(Uri userDefinedTypesUri, FeedOptions feedOptions = null)
        {
            if (userDefinedTypesUri == null)
            {
                throw new ArgumentNullException("userDefinedTypesUri");
            }
            return this.CreateUserDefinedTypeQuery(userDefinedTypesUri.OriginalString, feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user defined types in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypesUri">the URI to the user defined types.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        internal IQueryable<dynamic> CreateUserDefinedTypeQuery(Uri userDefinedTypesUri, string sqlExpression, FeedOptions feedOptions = null)
        {
            if (userDefinedTypesUri == null)
            {
                throw new ArgumentNullException("userDefinedTypesUri");
            }
            return this.CreateUserDefinedTypeQuery(userDefinedTypesUri, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Extension method to create a query for user defined types in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedTypesUri">the URI to the user defined types.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        internal IQueryable<dynamic> CreateUserDefinedTypeQuery(Uri userDefinedTypesUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            if (userDefinedTypesUri == null)
            {
                throw new ArgumentNullException("userDefinedTypesUri");
            }
            return this.CreateUserDefinedTypeQuery(userDefinedTypesUri.OriginalString, querySpec, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for user defined types under an Azure Cosmos DB database account
        /// in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">Specifies the database to read user defined types from.</param>
        /// <param name="feedOptions">Specifies the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        internal IDocumentQuery<UserDefinedType> CreateUserDefinedTypeChangeFeedQuery(Uri databaseUri, ChangeFeedOptions feedOptions)
        {
            if (databaseUri == null)
            {
                throw new ArgumentNullException(nameof(databaseUri));
            }

            return this.CreateUserDefinedTypeChangeFeedQuery(databaseUri.OriginalString, feedOptions);
        }
        #endregion Create Query
    }
}
