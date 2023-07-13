//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using System.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// The IDocumentClient interface captures the API signatures of the Azure Cosmos DB service .NET SDK.
    /// See <see cref="Microsoft.Azure.Cosmos.DocumentClient"/> for implementation details.
    /// </summary>
    internal interface IDocumentClient
    {
        #region Properties

        /// <summary>
        /// Gets or sets the session object used for session consistency version tracking in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// <value>
        /// The session object used for version tracking when the consistency level is set to Session.
        /// </value>
        /// The session object can be saved and shared between two DocumentClient instances within the same AppDomain.
        /// </remarks>
        object Session { get; set; }

        /// <summary>
        /// Gets the endpoint Uri for the service endpoint from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the service endpoint.
        /// </value>
        /// <seealso cref="System.Uri"/>
        Uri ServiceEndpoint { get; }

        /// <summary>
        /// Gets the current write endpoint chosen based on availability and preference in the Azure Cosmos DB service.
        /// </summary>
        Uri WriteEndpoint { get; }

        /// <summary>
        /// Gets the current read endpoint chosen based on availability and preference in the Azure Cosmos DB service.
        /// </summary>
        Uri ReadEndpoint { get; }

        /// <summary>
        /// Gets the Connection policy used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Connection policy used by the client.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.ConnectionPolicy"/>
        ConnectionPolicy ConnectionPolicy { get; }

        /// <summary>
        /// Gets the AuthKey used by the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The AuthKey used by the client.
        /// </value>
        /// <seealso cref="System.Security.SecureString"/>
        SecureString AuthKey { get; }

        /// <summary>
        /// Gets the configured consistency level of the client from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The configured <see cref="Microsoft.Azure.Cosmos.ConsistencyLevel"/> of the client.
        /// </value>
        /// <seealso cref="Microsoft.Azure.Cosmos.ConsistencyLevel"/>
        Documents.ConsistencyLevel ConsistencyLevel { get; }

        /// <summary>
        /// Gets Client Telemetry Instance with Task
        /// </summary>
        ClientTelemetry ClientTelemetryInstance { get; }
        
        #endregion

        #region Account operation

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.AccountProperties"/> as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        Task<AccountProperties> GetDatabaseAccountAsync();

        #endregion

        #region Create 

        /// <summary>
        /// Creates a database resource as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="database">The specification for the <see cref="Database"/> to create.</param>
        /// <param name="options">(Optional) The <see cref="Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The <see cref="Database"/> that was created within a task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="database"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Database are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the database object supplied. It is likely that an id was not supplied for the new Database.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Database"/> with an id matching the id field of <paramref name="database"/> already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// The example below creates a new <see cref="Database"/> with an Id property of 'MyDatabase'
        /// This code snippet is intended to be used from within an asynchronous method as it uses the await keyword
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = await client.CreateDatabaseAsync(new Database { Id = "MyDatabase" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// If you would like to construct a <see cref="Database"/> from within a synchronous method then you need to use the following code
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = client.CreateDatabaseAsync(new Database { Id = "MyDatabase" }).Result;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Documents.Database>> CreateDatabaseAsync(Documents.Database database, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates(if doesn't exist) or gets(if already exists) a database resource as an asychronous operation in the Azure Cosmos DB service.
        /// You can check the status code from the response to determine whether the database was newly created(201) or existing database was returned(200)
        /// </summary>
        /// <param name="database">The specification for the <see cref="Database"/> to create.</param>
        /// <param name="options">(Optional) The <see cref="Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The <see cref="Database"/> that was created within a task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="database"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property.</exception>
        /// <example>
        /// The example below creates a new <see cref="Database"/> with an Id property of 'MyDatabase'
        /// This code snippet is intended to be used from within an asynchronous method as it uses the await keyword
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "MyDatabase" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// If you would like to construct a <see cref="Database"/> from within a synchronous method then you need to use the following code
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Database db = client.CreateDatabaseIfNotExistsAsync(new Database { Id = "MyDatabase" }).Result;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Documents.Database>> CreateDatabaseIfNotExistsAsync(Documents.Database database, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">The link of the database to create the collection in. E.g. dbs/db_rid/.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="DocumentCollection"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="documentCollection"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a collection are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new collection.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for collections. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="DocumentCollection"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     //Create a new collection with an OfferThroughput set to 10000
        ///     //Not passing in RequestOptions.OfferThroughput will result in a collection with the default OfferThroughput set.
        ///     DocumentCollection coll = await client.CreateDocumentCollectionAsync(databaseLink,
        ///         new DocumentCollection { Id = "My Collection" },
        ///         new RequestOptions { OfferThroughput = 10000} );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionAsync(string databaseLink, DocumentCollection documentCollection, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates (if doesn't exist) or gets (if already exists) a collection as an asychronous operation in the Azure Cosmos DB service.
        /// You can check the status code from the response to determine whether the collection was newly created (201) or existing collection was returned (200).
        /// </summary>
        /// <param name="databaseLink">The link of the database to create the collection in. E.g. dbs/db_rid/.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="DocumentCollection"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="databaseLink"/> or <paramref name="documentCollection"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a DocumentCollection are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new collection.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for collections. Contact support to have this quota increased.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     //Create a new collection with an OfferThroughput set to 10000
        ///     //Not passing in RequestOptions.OfferThroughput will result in a collection with the default OfferThroughput set.
        ///     DocumentCollection coll = await client.CreateDocumentCollectionIfNotExistsAsync(databaseLink,
        ///         new DocumentCollection { Id = "My Collection" },
        ///         new RequestOptions { OfferThroughput = 10000} );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionIfNotExistsAsync(string databaseLink, DocumentCollection documentCollection, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI of the database to create the collection in.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionAsync(
            Uri databaseUri,
            DocumentCollection documentCollection,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates (if doesn't exist) or gets (if already exists) a collection as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">the URI of the database to create the collection in.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> object.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> you wish to provide when creating a Collection. E.g. RequestOptions.OfferThroughput = 400. </param>
        /// <returns>The <see cref="DocumentCollection"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<DocumentCollection>> CreateDocumentCollectionIfNotExistsAsync(
            Uri databaseUri,
            DocumentCollection documentCollection,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a Document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to create the document in. E.g. dbs/db_rid/colls/coll_rid/ </param>
        /// <param name="document">The document object to create.</param>
        /// <param name="options">(Optional) Any request options you wish to set. E.g. Specifying a Trigger to execute when creating the document. <see cref="Documents.Client.RequestOptions"/></param>
        /// <param name="disableAutomaticIdGeneration">(Optional) Disables the automatic id generation, If this is True the system will throw an exception if the id property is missing from the Document.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The <see cref="Microsoft.Azure.Documents.Document"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied. It is likely that <paramref name="disableAutomaticIdGeneration"/> was true and an id was not supplied</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to create the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Document"/> with an id matching the id field of <paramref name="document"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the <see cref="Document"/> exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Azure Cosmos DB supports a number of different ways to work with documents. A document can extend <see cref="Resource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyObject : Resource
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyObject { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A document can be any POCO object that can be serialized to JSON, even if it doesn't extend from <see cref="Resource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPOCO
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyPOCO { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Finally, a Document can also be a dynamic object
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync("dbs/db_rid/colls/coll_rid/", new { SomeProperty = "A Value" } );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Create a Document and execute a Pre and Post Trigger
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.CreateDocumentAsync(
        ///         "dbs/db_rid/colls/coll_rid/",
        ///         new { id = "DOC123213443" },
        ///         new RequestOptions
        ///         {
        ///             PreTriggerInclude = new List<string> { "MyPreTrigger" },
        ///             PostTriggerInclude = new List<string> { "MyPostTrigger" }
        ///         });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Document>> CreateDocumentAsync(string collectionLink, object document, Documents.Client.RequestOptions options = null, bool disableAutomaticIdGeneration = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to create the document in.</param>
        /// <param name="document">The document object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="disableAutomaticIdGeneration">A flag to disable automatic id generation.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Document>> CreateDocumentAsync(
            Uri documentCollectionUri,
            object document,
            Documents.Client.RequestOptions options = null,
            bool disableAutomaticIdGeneration = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the collection to create the stored procedure in. E.g. dbs/db_rid/colls/col_rid/</param>
        /// <param name="storedProcedure">The <see cref="StoredProcedure"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>The <see cref="StoredProcedure"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the stored procedure or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of stored procedures for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="StoredProcedure"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="StoredProcedure"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a new stored procedure called "HelloWorldSproc" that takes in a single param called "name".
        /// StoredProcedure sproc = await client.CreateStoredProcedureAsync(collectionLink, new StoredProcedure
        /// {
        ///    Id = "HelloWorldSproc",
        ///    Body = @"function (name){
        ///                var response = getContext().getResponse();
        ///                response.setBody('Hello ' + name);
        ///             }"
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<StoredProcedure>> CreateStoredProcedureAsync(string collectionLink, StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to create the stored procedure in.</param>
        /// <param name="storedProcedure">The <see cref="StoredProcedure"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<StoredProcedure>> CreateStoredProcedureAsync(
            Uri documentCollectionUri,
            StoredProcedure storedProcedure,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to create the trigger in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="trigger">The <see cref="Trigger"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Trigger"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Trigger"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a trigger that validates the contents of a document as it is created and adds a 'timestamp' property if one was not found.
        /// Trigger trig = await client.CreateTriggerAsync(collectionLink, new Trigger
        /// {
        ///     Id = "ValidateDocuments",
        ///     Body = @"function validate() {
        ///                         var context = getContext();
        ///                         var request = context.getRequest();                                                             
        ///                         var documentToCreate = request.getBody();
        ///                         
        ///                         // validate properties
        ///                         if (!('timestamp' in documentToCreate)) {
        ///                             var ts = new Date();
        ///                             documentToCreate['timestamp'] = ts.getTime();
        ///                         }
        ///                         
        ///                         // update the document that will be created
        ///                         request.setBody(documentToCreate);
        ///                       }",
        ///     TriggerType = TriggerType.Pre,
        ///     TriggerOperation = TriggerOperation.Create
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Trigger>> CreateTriggerAsync(string collectionLink, Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to create the trigger in.</param>
        /// <param name="trigger">The <see cref="Trigger"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Trigger>> CreateTriggerAsync(Uri documentCollectionUri, Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to create the user defined function in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="function">The <see cref="UserDefinedFunction"/> object to create.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="function"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="UserDefinedFunction"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="UserDefinedFunction"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Create a user defined function that converts a string to upper case
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction
        /// {
        ///    Id = "ToUpper",
        ///    Body = @"function toUpper(input) {
        ///                        return input.toUpperCase();
        ///                     }",
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<UserDefinedFunction>> CreateUserDefinedFunctionAsync(string collectionLink, UserDefinedFunction function, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Creates a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to create the user defined function in.</param>
        /// <param name="function">The <see cref="UserDefinedFunction"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<UserDefinedFunction>> CreateUserDefinedFunctionAsync(
            Uri documentCollectionUri,
            UserDefinedFunction function,
            Documents.Client.RequestOptions options = null);

        #endregion

        #region Delete 
        /// <summary>
        /// Delete a <see cref="Database"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="databaseLink">The link of the <see cref="Database"/> to delete. E.g. dbs/db_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="databaseLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a database using its selfLink property
        /// //To get the databaseLink you would have to query for the Database, using CreateDatabaseQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDatabaseAsync(databaseLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Documents.Database>> DeleteDatabaseAsync(string databaseLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a database as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI of the database to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Documents.Database>> DeleteDatabaseAsync(Uri databaseUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a <see cref="DocumentCollection"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollectionLink">The link of the <see cref="Microsoft.Azure.Documents.Document"/> to delete. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentCollectionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a collection using its selfLink property
        /// //To get the collectionLink you would have to query for the Collection, using CreateDocumentCollectionQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDocumentCollectionAsync(collectionLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<DocumentCollection>> DeleteDocumentCollectionAsync(string documentCollectionLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<DocumentCollection>> DeleteDocumentCollectionAsync(Uri documentCollectionUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Documents.Document"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link of the <see cref="Microsoft.Azure.Documents.Document"/> to delete. E.g. dbs/db_rid/colls/col_rid/docs/doc_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a document using its selfLink property
        /// //To get the documentLink you would have to query for the Document, using CreateDocumentQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteDocumentAsync(documentLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Document>> DeleteDocumentAsync(string documentLink, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a document as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">The URI of the document to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Document>> DeleteDocumentAsync(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="StoredProcedure"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureLink">The link of the <see cref="StoredProcedure"/> to delete. E.g. dbs/db_rid/colls/col_rid/sprocs/sproc_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a stored procedure using its selfLink property.
        /// //To get the sprocLink you would have to query for the Stored Procedure, using CreateStoredProcedureQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteStoredProcedureAsync(sprocLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<StoredProcedure>> DeleteStoredProcedureAsync(string storedProcedureLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureUri">The URI of the stored procedure to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<StoredProcedure>> DeleteStoredProcedureAsync(Uri storedProcedureUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a <see cref="Trigger"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerLink">The link of the <see cref="Trigger"/> to delete. E.g. dbs/db_rid/colls/col_rid/triggers/trigger_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a trigger using its selfLink property.
        /// //To get the triggerLink you would have to query for the Trigger, using CreateTriggerQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteTriggerAsync(triggerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Trigger>> DeleteTriggerAsync(string triggerLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerUri">The URI of the trigger to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Trigger>> DeleteTriggerAsync(Uri triggerUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a <see cref="UserDefinedFunction"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="functionLink">The link of the <see cref="UserDefinedFunction"/> to delete. E.g. dbs/db_rid/colls/col_rid/udfs/udf_rid/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="functionLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a user defined function using its selfLink property.
        /// //To get the functionLink you would have to query for the User Defined Function, using CreateUserDefinedFunctionQuery(),  and then refer to its .SelfLink property
        /// await client.DeleteUserDefinedFunctionAsync(functionLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<UserDefinedFunction>> DeleteUserDefinedFunctionAsync(string functionLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="functionUri">The URI of the user defined function to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<UserDefinedFunction>> DeleteUserDefinedFunctionAsync(Uri functionUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a <see cref="Microsoft.Azure.Documents.Conflict"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictLink">The link of the <see cref="Microsoft.Azure.Documents.Conflict"/> to delete. E.g. dbs/db_rid/colls/coll_rid/conflicts/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Delete a conflict using its selfLink property.
        /// //To get the conflictLink you would have to query for the Conflict object, using CreateConflictQuery(), and then refer to its .SelfLink property
        /// await client.DeleteConflictAsync(conflictLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Conflict>> DeleteConflictAsync(string conflictLink, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Delete a conflict as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflictUri">The URI of the conflict to delete.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Conflict>> DeleteConflictAsync(Uri conflictUri, Documents.Client.RequestOptions options = null);

        #endregion

        #region Replace 

        /// <summary>
        /// Replaces a document collection in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollection">the updated document collection.</param>
        /// <param name="options">the request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="DocumentCollection"/> containing the updated resource record.
        /// </returns>
        Task<ResourceResponse<DocumentCollection>> ReplaceDocumentCollectionAsync(DocumentCollection documentCollection, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a document collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to be updated.</param>
        /// <param name="documentCollection">The updated document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<DocumentCollection>> ReplaceDocumentCollectionAsync(
            Uri documentCollectionUri,
            DocumentCollection documentCollection,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Documents.Document"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link of the document to be updated. E.g. dbs/db_rid/colls/col_rid/docs/doc_rid/ </param>
        /// <param name="document">The updated <see cref="Microsoft.Azure.Documents.Document"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="documentLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// In this example, instead of using a strongly typed <see cref="Document"/>, we will work with our own POCO object and not rely on the dynamic nature of the Document class.
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPoco
        /// {
        ///     public string Id {get; set;}
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// //Get the doc back as a Document so you have access to doc.SelfLink
        /// Document doc = client.CreateDocumentQuery<Document>(collectionLink)
        ///                        .Where(r => r.Id == "doc id")
        ///                        .AsEnumerable()
        ///                        .SingleOrDefault();
        ///
        /// //Now dynamically cast doc back to your MyPoco
        /// MyPoco poco = (dynamic)doc;
        ///
        /// //Update some properties of the poco object
        /// poco.MyProperty = "updated value";
        ///
        /// //Now persist these changes to the database using doc.SelLink and the update poco object
        /// Document updated = await client.ReplaceDocumentAsync(doc.SelfLink, poco);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Document>> ReplaceDocumentAsync(string documentLink, object document, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a document as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">The URI of the document to be updated.</param>
        /// <param name="document">The updated document.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Document>> ReplaceDocumentAsync(Uri documentUri, object document, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Documents.Document"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="document">The updated <see cref="Microsoft.Azure.Documents.Document"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="document"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// This example uses <see cref="Document"/> and takes advantage of the fact that it is a dynamic object and uses SetProperty to dynamically update properties on the document
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the Document to be updated
        /// Document doc = client.CreateDocumentQuery<Document>(collectionLink)
        ///                             .Where(r => r.Id == "doc id")
        ///                             .AsEnumerable()
        ///                             .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// doc.SetPropertyValue("MyProperty", "updated value");
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Document updated = await client.ReplaceDocumentAsync(doc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Document>> ReplaceDocumentAsync(Document document, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a <see cref="StoredProcedure"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedure">The updated <see cref="StoredProcedure"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="StoredProcedure"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// StoredProcedure sproc = client.CreateStoredProcedureQuery(sprocsLink)
        ///                                  .Where(r => r.Id == "sproc id")
        ///                                  .AsEnumerable()
        ///                                  .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// sproc.Body = "function () {new javascript body for sproc}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// StoredProcedure updated = await client.ReplaceStoredProcedureAsync(sproc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<StoredProcedure>> ReplaceStoredProcedureAsync(StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replace the specified stored procedure in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureUri">The URI for the stored procedure to be updated.</param>
        /// <param name="storedProcedure">The updated stored procedure.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<StoredProcedure>> ReplaceStoredProcedureAsync(
            Uri storedProcedureUri,
            StoredProcedure storedProcedure,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a <see cref="Trigger"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="trigger">The updated <see cref="Trigger"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Trigger"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// Trigger trigger = client.CreateTriggerQuery(sprocsLink)
        ///                               .Where(r => r.Id == "trigger id")
        ///                               .AsEnumerable()
        ///                               .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// trigger.Body = "function () {new javascript body for trigger}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Trigger updated = await client.ReplaceTriggerAsync(sproc);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Trigger>> ReplaceTriggerAsync(Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerUri">The URI for the trigger to be updated.</param>
        /// <param name="trigger">The updated trigger.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Trigger>> ReplaceTriggerAsync(Uri triggerUri, Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a <see cref="UserDefinedFunction"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="function">The updated <see cref="UserDefinedFunction"/> to replace the existing resource with.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="UserDefinedFunction"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="function"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(functionsLink)
        ///                                     .Where(r => r.Id == "udf id")
        ///                                     .AsEnumerable()
        ///                                     .SingleOrDefault();
        ///
        /// //Update some properties on the found resource
        /// udf.Body = "function () {new javascript body for udf}";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// UserDefinedFunction updated = await client.ReplaceUserDefinedFunctionAsync(udf);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<UserDefinedFunction>> ReplaceUserDefinedFunctionAsync(UserDefinedFunction function, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionUri">The URI for the user defined function to be updated.</param>
        /// <param name="function">The updated user defined function.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<UserDefinedFunction>> ReplaceUserDefinedFunctionAsync(
            Uri userDefinedFunctionUri,
            UserDefinedFunction function,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Replaces a <see cref="Microsoft.Azure.Documents.Offer"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="offer">The updated <see cref="Microsoft.Azure.Documents.Offer"/> to replace the existing resource with.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Offer"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="offer"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Fetch the resource to be updated
        /// Offer offer = client.CreateOfferQuery()
        ///                          .Where(r => r.ResourceLink == "collection selfLink")
        ///                          .AsEnumerable()
        ///                          .SingleOrDefault();
        ///
        /// //Change the user mode to All
        /// offer.OfferType = "S3";
        ///
        /// //Now persist these changes to the database by replacing the original resource
        /// Offer updated = await client.ReplaceOfferAsync(offer);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Offer>> ReplaceOfferAsync(Offer offer);

        #endregion

        #region Read 

        /// <summary>
        /// Reads a <see cref="Database"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="databaseLink">The link of the Database resource to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Database"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="databaseLink"/> is not set.</exception>
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
        /// // - database_id is the ID property of the Database resource you wish to read.
        /// var dbLink = "/dbs/database_id";
        /// Database database = await client.ReadDatabaseAsync(dbLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Database if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="databaseLink"/> is always "/dbs/{db identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<Documents.Database>> ReadDatabaseAsync(string databaseLink, Documents.Client.RequestOptions options = null);

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
        /// <seealso cref="Documents.Database"/> 
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Documents.Database>> ReadDatabaseAsync(Uri databaseUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads a <see cref="DocumentCollection"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentCollectionLink">The link for the DocumentCollection to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="DocumentCollection"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentCollectionLink"/> is not set.</exception>
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
        /// //This reads a DocumentCollection record from a database where
        /// // - sample_database is the ID of the database
        /// // - collection_id is the ID of the collection resource to be read
        /// var collLink = "/dbs/sample_database/colls/collection_id";
        /// DocumentCollection coll = await client.ReadDocumentCollectionAsync(collLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the DocumentCollection if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentCollectionLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<DocumentCollection>> ReadDocumentCollectionAsync(string documentCollectionLink, Documents.Client.RequestOptions options = null);

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
        Task<ResourceResponse<DocumentCollection>> ReadDocumentCollectionAsync(Uri documentCollectionUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link for the document to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
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
        /// //This reads a document record from a database & collection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - document_id is the ID of the document resource
        /// var docLink = "dbs/sample_database/colls/sample_collection/docs/document_id";
        /// Document doc = await client.ReadDocumentAsync(docLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Document if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentLink"/> is always "dbs/{db identifier}/colls/{coll identifier}/docs/{doc identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<Document>> ReadDocumentAsync(string documentLink, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> as an asynchronous operation from the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentUri">A URI to the Document resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
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
        Task<ResourceResponse<Document>> ReadDocumentAsync(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> as a generic type T from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentLink">The link for the document to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.DocumentResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Document"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentLink"/> is not set.</exception>
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
        /// //This reads a document record from a database & collection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - document_id is the ID of the document resource
        /// var docLink = "dbs/sample_database/colls/sample_collection/docs/document_id";
        /// Customer customer = await client.ReadDocumentAsync<Customer>(docLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Document if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="documentLink"/> is always "dbs/{db identifier}/colls/{coll identifier}/docs/{doc identifier}" only
        /// the values within the {} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.DocumentResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<DocumentResponse<T>> ReadDocumentAsync<T>(string documentLink, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Document"/> as a generic type T from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="documentUri">A URI to the Document resource to be read.</param>
        /// <param name="options">The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
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
        Task<DocumentResponse<T>> ReadDocumentAsync<T>(Uri documentUri, Documents.Client.RequestOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a <see cref="StoredProcedure"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureLink">The link of the stored procedure to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="StoredProcedure"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
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
        /// //Reads a StoredProcedure from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - sproc_id is the ID of the stored procedure to be read
        /// var sprocLink = "/dbs/sample_database/colls/sample_collection/sprocs/sproc_id";
        /// StoredProcedure sproc = await client.ReadStoredProcedureAsync(sprocLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Stored Procedure if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="storedProcedureLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/sprocs/{sproc identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<StoredProcedure>> ReadStoredProcedureAsync(string storedProcedureLink, Documents.Client.RequestOptions options = null);

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
        Task<ResourceResponse<StoredProcedure>> ReadStoredProcedureAsync(Uri storedProcedureUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads a <see cref="Trigger"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerLink">The link to the Trigger to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Trigger"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerLink"/> is not set.</exception>
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
        /// //Reads a Trigger from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - trigger_id is the ID of the trigger to be read
        /// var triggerLink = "/dbs/sample_database/colls/sample_collection/triggers/trigger_id";
        /// Trigger trigger = await client.ReadTriggerAsync(triggerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Trigger if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="triggerLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/triggers/{trigger identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<Trigger>> ReadTriggerAsync(string triggerLink, Documents.Client.RequestOptions options = null);

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
        Task<ResourceResponse<Trigger>> ReadTriggerAsync(Uri triggerUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads a <see cref="UserDefinedFunction"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="functionLink">The link to the User Defined Function to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="UserDefinedFunction"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="functionLink"/> is not set.</exception>
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
        /// //Reads a User Defined Function from a Database and DocumentCollection where
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - udf_id is the ID of the user-defined function to be read
        /// var udfLink = "/dbs/sample_database/colls/sample_collection/udfs/udf_id";
        /// UserDefinedFunction udf = await client.ReadUserDefinedFunctionAsync(udfLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the User Defined Function if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="functionLink"/> is always "/dbs/{db identifier}/colls/{coll identifier}/udfs/{udf identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<UserDefinedFunction>> ReadUserDefinedFunctionAsync(string functionLink, Documents.Client.RequestOptions options = null);

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
        Task<ResourceResponse<UserDefinedFunction>> ReadUserDefinedFunctionAsync(Uri functionUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads a <see cref="Microsoft.Azure.Documents.Conflict"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictLink">The link to the Conflict to be read.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Conflict"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictLink"/> is not set.</exception>
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
        /// //Reads a Conflict resource from a Database
        /// // - sample_database is the ID of the database
        /// // - sample_collection is the ID of the collection
        /// // - conflict_id is the ID of the conflict to be read
        /// var conflictLink = "/dbs/sample_database/colls/sample_collection/conflicts/conflict_id";
        /// Conflict conflict = await client.ReadConflictAsync(conflictLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// The example shown uses ID-based links, where the link is composed of the ID properties used when the resources were created.
        /// You can still use the <see cref="Documents.Resource.SelfLink"/> property of the Conflict if you prefer. A self-link is a URI for a resource that is made up of Resource Identifiers  (or the _rid properties).
        /// ID-based links and SelfLink will both work.
        /// The format for <paramref name="conflictLink"/> is always "/dbs/{db identifier}/colls/{collectioon identifier}/conflicts/{conflict identifier}"
        /// only the values within the {...} change depending on which method you wish to use to address the resource.
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<Conflict>> ReadConflictAsync(string conflictLink, Documents.Client.RequestOptions options = null);

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
        Task<ResourceResponse<Conflict>> ReadConflictAsync(Uri conflictUri, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Reads an <see cref="Microsoft.Azure.Documents.Offer"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="offerLink">The link to the Offer to be read.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Offer"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="offerLink"/> is not set.</exception>
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
        /// //Reads an Offer resource from a Database
        /// // - offer_id is the ID of the conflict to be read
        /// var offerLink = "/offers/offer_id";
        /// Offer offer = await client.ReadOfferAsync(offerLink);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// <para>
        /// For an Offer, id is always generated internally by the system when the linked resource is created. id and _rid are always the same for Offer.
        /// </para>
        /// <para>
        /// The format for <paramref name="offerLink"/> is always "/offers/{offer identifier}"
        /// </para>
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        /// <seealso cref="System.Uri"/>
        Task<ResourceResponse<Offer>> ReadOfferAsync(string offerLink);

        #endregion

        #region ReadFeed 

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Database"/> for a database account from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Database"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<Database> response = await client.ReadDatabaseFeedAsync(new FeedOptions
        ///                                                                 {
        ///                                                                     MaxItemCount = 10,
        ///                                                                     RequestContinuation = continuation
        ///                                                                 });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<Documents.Database>> ReadDatabaseFeedAsync(FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Documents.PartitionKeyRange"/> for a database account from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKeyRangesOrCollectionLink">The link of the resources to be read, or owner collection link, SelfLink or AltLink. E.g. /dbs/db_rid/colls/coll_rid/pkranges</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Database"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// DoucmentFeedResponse<PartitionKeyRange> response = null;
        /// List<string> ids = new List<string>();
        /// do
        /// {
        ///     response = await client.ReadPartitionKeyRangeFeedAsync(collection.SelfLink, new FeedOptions { MaxItemCount = 1000 });
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
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedAsync(string partitionKeyRangesOrCollectionLink, FeedOptions options = null);

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
        Task<DocumentFeedResponse<PartitionKeyRange>> ReadPartitionKeyRangeFeedAsync(Uri partitionKeyRangesOrCollectionUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="DocumentCollection"/> for a database from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="collectionsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="DocumentCollection"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="collectionsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<DocumentCollection> response = await client.ReadDocumentCollectionFeedAsync("/dbs/db_rid/colls/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<DocumentCollection>> ReadDocumentCollectionFeedAsync(string collectionsLink, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of collections for a database as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI of the parent Database.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<DocumentCollection>> ReadDocumentCollectionFeedAsync(Uri databaseUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of documents for a specified collection from the Azure Cosmos DB service.
        /// This takes returns a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which will contain an enumerable list of dynamic objects.
        /// </summary>
        /// <param name="documentsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/coll_rid/docs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> containing dynamic objects representing the items in the feed.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="documentsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<dynamic> response = await client.ReadDocumentFeedAsync("/dbs/db_rid/colls/coll_rid/docs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Instead of DoucmentFeedResponse{Document} this method takes advantage of dynamic objects in .NET. This way a single feed result can contain any kind of Document, or POCO object.
        /// This is important becuse a DocumentCollection can contain different kinds of documents.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<dynamic>> ReadDocumentFeedAsync(string documentsLink, FeedOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the feed (sequence) of documents for a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<dynamic>> ReadDocumentFeedAsync(Uri documentCollectionUri, FeedOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="StoredProcedure"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProceduresLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/sprocs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="StoredProcedure"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProceduresLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<StoredProcedure> response = await client.ReadStoredProcedureFeedAsync("/dbs/db_rid/colls/col_rid/sprocs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<StoredProcedure>> ReadStoredProcedureFeedAsync(string storedProceduresLink, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of stored procedures for a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<StoredProcedure>> ReadStoredProcedureFeedAsync(Uri documentCollectionUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Trigger"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="triggersLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/triggers/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Trigger"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggersLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<Trigger> response = await client.ReadTriggerFeedAsync("/dbs/db_rid/colls/col_rid/triggers/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<Trigger>> ReadTriggerFeedAsync(string triggersLink, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of triggers for a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<Trigger>> ReadTriggerFeedAsync(Uri documentCollectionUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="UserDefinedFunction"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/col_rid/udfs/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="UserDefinedFunction"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedFunctionsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<UserDefinedFunction> response = await client.ReadUserDefinedFunctionFeedAsync("/dbs/db_rid/colls/col_rid/udfs/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<UserDefinedFunction>> ReadUserDefinedFunctionFeedAsync(string userDefinedFunctionsLink, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of user defined functions for a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<UserDefinedFunction>> ReadUserDefinedFunctionFeedAsync(Uri documentCollectionUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Documents.Conflict"/> for a collection from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="conflictsLink">The SelfLink of the resources to be read. E.g. /dbs/db_rid/colls/coll_rid/conflicts/ </param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Conflict"/> containing the read resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conflictsLink"/> is not set.</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource feed you tried to read did not exist. Check the parent rids are correct.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<Conflict> response = await client.ReadConflictAsync("/dbs/db_rid/colls/coll_rid/conflicts/",
        ///                                                     new FeedOptions
        ///                                                     {
        ///                                                         MaxItemCount = 10,
        ///                                                         RequestContinuation = continuation
        ///                                                     });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<Conflict>> ReadConflictFeedAsync(string conflictsLink, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of conflicts for a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentFeedResponse<Conflict>> ReadConflictFeedAsync(Uri documentCollectionUri, FeedOptions options = null);

        /// <summary>
        /// Reads the feed (sequence) of <see cref="Microsoft.Azure.Documents.Offer"/> for a database account from the Azure Cosmos DB service
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks"/> containing a <see cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/> which wraps a <see cref="Microsoft.Azure.Documents.Offer"/> containing the read resource record.
        /// </returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// int count = 0;
        /// string continuation = string.Empty;
        /// do
        /// {
        ///     // Read the feed 10 items at a time until there are no more items to read
        ///     DoucmentFeedResponse<Offer> response = await client.ReadOfferAsync(new FeedOptions
        ///                                                                 {
        ///                                                                     MaxItemCount = 10,
        ///                                                                     RequestContinuation = continuation
        ///                                                                 });
        ///
        ///     // Append the item count
        ///     count += response.Count;
        ///
        ///     // Get the continuation so that we know when to stop.
        ///      continuation = response.ResponseContinuation;
        /// } while (!string.IsNullOrEmpty(continuation));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<DocumentFeedResponse<Offer>> ReadOffersFeedAsync(FeedOptions options = null);

        #endregion

        #region Execute Stored procs

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, params dynamic[] procedureParams);

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">The URI of the stored procedure to be executed.</param>
        /// <param name="procedureParams">The parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(Uri storedProcedureUri, params dynamic[] procedureParams);

        /// <summary>
        /// Executes a stored procedure against a partitioned collection in the Azure Cosmos DB service as an asynchronous operation, specifiying a target partition.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new RequestOptions { PartitionKey = new PartitionKey(1) },
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, Documents.Client.RequestOptions options, params dynamic[] procedureParams);

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">The URI of the stored procedure to be executed.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="procedureParams">The parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(
            Uri storedProcedureUri,
            Documents.Client.RequestOptions options,
            params dynamic[] procedureParams);

        /// <summary>
        /// Executes a stored procedure against a partitioned collection in the Azure Cosmos DB service as an asynchronous operation, specifiying a target partition.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureLink">The link to the stored procedure to execute.</param>
        /// <param name="options">(Optional) The request options for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="procedureParams">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureLink"/> is not set.</exception>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// //Execute a StoredProcedure with ResourceId of "sproc_rid" that takes two "Player" documents, does some stuff, and returns a bool
        /// StoredProcedureResponse<bool> sprocResponse = await client.ExecuteStoredProcedureAsync<bool>(
        ///                                                         "/dbs/db_rid/colls/col_rid/sprocs/sproc_rid/",
        ///                                                         new RequestOptions { PartitionKey = new PartitionKey(1) },
        ///                                                         new Player { id="1", name="joe" } ,
        ///                                                         new Player { id="2", name="john" }
        ///                                                     );
        ///
        /// if (sprocResponse.Response) Console.WriteLine("Congrats, the stored procedure did some stuff");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.StoredProcedureResponse{TValue}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(string storedProcedureLink, Documents.Client.RequestOptions options, CancellationToken cancellationToken, params dynamic[] procedureParams);

        /// <summary>
        /// Executes a stored procedure against a collection as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="TValue">The type of the stored procedure's return value.</typeparam>
        /// <param name="storedProcedureUri">The URI of the stored procedure to be executed.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="procedureParams">The parameters for the stored procedure execution.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<StoredProcedureResponse<TValue>> ExecuteStoredProcedureAsync<TValue>(
            Uri storedProcedureUri,
            Documents.Client.RequestOptions options,
            CancellationToken cancellationToken,
            params dynamic[] procedureParams);

        #endregion

        #region Upsert 
        /// <summary>
        /// Upserts a Document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to upsert the document in. E.g. dbs/db_rid/colls/coll_rid/ </param>
        /// <param name="document">The document object to upsert.</param>
        /// <param name="options">(Optional) Any request options you wish to set. E.g. Specifying a Trigger to execute when creating the document. <see cref="Documents.Client.RequestOptions"/></param>
        /// <param name="disableAutomaticIdGeneration">(Optional) Disables the automatic id generation, If this is True the system will throw an exception if the id property is missing from the Document.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The <see cref="Document"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="document"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied. It is likely that <paramref name="disableAutomaticIdGeneration"/> was true and an id was not supplied</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to upsert the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Document"/> with an id matching the id field of <paramref name="document"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the <see cref="Document"/> exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Azure Cosmos DB supports a number of different ways to work with documents. A document can extend <see cref="Resource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyObject : Resource
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyObject { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A document can be any POCO object that can be serialized to JSON, even if it doesn't extend from <see cref="Resource"/>
        /// <code language="c#">
        /// <![CDATA[
        /// public class MyPOCO
        /// {
        ///     public string MyProperty {get; set;}
        /// }
        ///
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new MyPOCO { MyProperty = "A Value" });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// A Document can also be a dynamic object
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync("dbs/db_rid/colls/coll_rid/", new { SomeProperty = "A Value" } );
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// Upsert a Document and execute a Pre and Post Trigger
        /// <code language="c#">
        /// <![CDATA[
        /// using (IDocumentClient client = new DocumentClient(new Uri("service endpoint"), "auth key"))
        /// {
        ///     Document doc = await client.UpsertDocumentAsync(
        ///         "dbs/db_rid/colls/coll_rid/",
        ///         new { id = "DOC123213443" },
        ///         new RequestOptions
        ///         {
        ///             PreTriggerInclude = new List<string> { "MyPreTrigger" },
        ///             PostTriggerInclude = new List<string> { "MyPostTrigger" }
        ///         });
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Document>> UpsertDocumentAsync(string collectionLink, object document, Documents.Client.RequestOptions options = null, bool disableAutomaticIdGeneration = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Upserts a document as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to upsert the document in.</param>
        /// <param name="document">The document object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <param name="disableAutomaticIdGeneration">A flag to disable the automatic id generation.</param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Document>> UpsertDocumentAsync(
            Uri documentCollectionUri,
            object document,
            Documents.Client.RequestOptions options = null,
            bool disableAutomaticIdGeneration = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upserts a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the collection to upsert the stored procedure in. E.g. dbs/db_rid/colls/col_rid/</param>
        /// <param name="storedProcedure">The <see cref="StoredProcedure"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>The <see cref="StoredProcedure"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="storedProcedure"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the stored procedure or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of stored procedures for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="StoredProcedure"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="StoredProcedure"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a new stored procedure called "HelloWorldSproc" that takes in a single param called "name".
        /// StoredProcedure sproc = await client.UpsertStoredProcedureAsync(collectionLink, new StoredProcedure
        /// {
        ///    Id = "HelloWorldSproc",
        ///    Body = @"function (name){
        ///                var response = getContext().getResponse();
        ///                response.setBody('Hello ' + name);
        ///             }"
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<StoredProcedure>> UpsertStoredProcedureAsync(string collectionLink, StoredProcedure storedProcedure, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Upserts a stored procedure as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to upsert the stored procedure in.</param>
        /// <param name="storedProcedure">The <see cref="StoredProcedure"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<StoredProcedure>> UpsertStoredProcedureAsync(
            Uri documentCollectionUri,
            StoredProcedure storedProcedure,
            Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Upserts a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to upsert the trigger in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="trigger">The <see cref="Trigger"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="trigger"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="Trigger"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="Trigger"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a trigger that validates the contents of a document as it is created and adds a 'timestamp' property if one was not found.
        /// Trigger trig = await client.UpsertTriggerAsync(collectionLink, new Trigger
        /// {
        ///     Id = "ValidateDocuments",
        ///     Body = @"function validate() {
        ///                         var context = getContext();
        ///                         var request = context.getRequest();                                                             
        ///                         var documentToCreate = request.getBody();
        ///                         
        ///                         // validate properties
        ///                         if (!('timestamp' in documentToCreate)) {
        ///                             var ts = new Date();
        ///                             documentToCreate['timestamp'] = ts.getTime();
        ///                         }
        ///                         
        ///                         // update the document that will be created
        ///                         request.setBody(documentToCreate);
        ///                       }",
        ///     TriggerType = TriggerType.Pre,
        ///     TriggerOperation = TriggerOperation.Create
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<Trigger>> UpsertTriggerAsync(string collectionLink, Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Upserts a trigger as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to upsert the trigger in.</param>
        /// <param name="trigger">The <see cref="Trigger"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<Trigger>> UpsertTriggerAsync(Uri documentCollectionUri, Trigger trigger, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Upserts a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">The link of the <see cref="DocumentCollection"/> to upsert the user defined function in. E.g. dbs/db_rid/colls/col_rid/ </param>
        /// <param name="function">The <see cref="UserDefinedFunction"/> object to upsert.</param>
        /// <param name="options">(Optional) Any <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/>for this request.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="collectionLink"/> or <paramref name="function"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occured during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="UserDefinedFunction"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="UserDefinedFunction"/> you tried to upsert was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// //Upsert a user defined function that converts a string to upper case
        /// UserDefinedFunction udf = client.UpsertUserDefinedFunctionAsync(collectionLink, new UserDefinedFunction
        /// {
        ///    Id = "ToUpper",
        ///    Body = @"function toUpper(input) {
        ///                        return input.toUpperCase();
        ///                     }",
        /// });
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.RequestOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.Client.ResourceResponse{T}"/>
        /// <seealso cref="System.Threading.Tasks.Task"/>
        Task<ResourceResponse<UserDefinedFunction>> UpsertUserDefinedFunctionAsync(string collectionLink, UserDefinedFunction function, Documents.Client.RequestOptions options = null);

        /// <summary>
        /// Upserts a user defined function as an asychronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection to upsert the user defined function in.</param>
        /// <param name="function">The <see cref="UserDefinedFunction"/> object.</param>
        /// <param name="options">(Optional) The <see cref="Microsoft.Azure.Documents.Client.RequestOptions"/> for the request.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<ResourceResponse<UserDefinedFunction>> UpsertUserDefinedFunctionAsync(
            Uri documentCollectionUri,
            UserDefinedFunction function,
            Documents.Client.RequestOptions options = null);

        #endregion

        #region Create Query

        /// <summary>
        /// Method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI of the parent database.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<DocumentCollection> CreateDocumentCollectionQuery(Uri databaseUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI to the database.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateDocumentCollectionQuery(Uri databaseUri, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for document collections in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseUri">The URI to the database.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateDocumentCollectionQuery(Uri databaseUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<StoredProcedure> CreateStoredProcedureQuery(Uri documentCollectionUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateStoredProcedureQuery(Uri documentCollectionUri, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for stored procedures in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateStoredProcedureQuery(Uri documentCollectionUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<Trigger> CreateTriggerQuery(Uri documentCollectionUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateTriggerQuery(Uri documentCollectionUri, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create query for triggers in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateTriggerQuery(Uri documentCollectionUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<UserDefinedFunction> CreateUserDefinedFunctionQuery(Uri documentCollectionUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateUserDefinedFunctionQuery(Uri documentCollectionUri, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for user-defined functions in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateUserDefinedFunctionQuery(Uri documentCollectionUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<Conflict> CreateConflictQuery(Uri documentCollectionUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateConflictQuery(Uri documentCollectionUri, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for conflicts in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateConflictQuery(Uri documentCollectionUri, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionUri">The URI of the parent document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<T> CreateDocumentQuery<T>(Uri documentCollectionUri, FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<T> CreateDocumentQuery<T>(
            Uri documentCollectionUri,
            string sqlExpression,
            FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<T> CreateDocumentQuery<T>(
            Uri documentCollectionUri,
            SqlQuerySpec querySpec,
            FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IOrderedQueryable<Document> CreateDocumentQuery(
            Uri documentCollectionUri,
            FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="sqlExpression">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateDocumentQuery(
            Uri documentCollectionUri,
            string sqlExpression,
            FeedOptions feedOptions = null);

        /// <summary>
        /// Method to create a query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="documentCollectionUri">The URI of the document collection.</param>
        /// <param name="querySpec">The sql query.</param>
        /// <param name="feedOptions">(Optional) The <see cref="Microsoft.Azure.Cosmos.FeedOptions"/> for processing the query results feed.</param>
        /// <returns>The query result set.</returns>
        IQueryable<dynamic> CreateDocumentQuery(
            Uri documentCollectionUri,
            SqlQuerySpec querySpec,
            FeedOptions feedOptions = null);

        /// <summary>
        /// Extension method to create a change feed query for documents in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">Specifies the collection to read documents from.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        IDocumentQuery<Document> CreateDocumentChangeFeedQuery(Uri collectionLink, ChangeFeedOptions feedOptions);
        #endregion

        #region Create Query with link

        /// <summary>
        /// Overloaded. This method creates a query for database resources under an account in the Azure Cosmos DB service. It returns An IOrderedQueryable{Database}.
        /// </summary>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Database} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Database database = client.CreateDatabaseQuery().Where(d => d.Id == "mydb").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<Documents.Database> CreateDatabaseQuery(FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for database resources under an Azure Cosmos DB database account by using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Database database = client.CreateDatabaseQuery("SELECT * FROM dbs d WHERE d.id = 'mydb'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Documents.Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDatabaseQuery(string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for database resources under an Azure Cosmos DB database account by using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM dbs d WHERE d.id = @id",
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "mydb" }}));
        /// dynamic database = client.CreateDatabaseQuery<dynamic>(query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDatabaseQuery(SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database. It returns An IOrderedQueryable{DocumentCollection}.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{DocumentCollection} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink).Where(c => c.Id == "myColl").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<DocumentCollection> CreateDocumentCollectionQuery(string databaseLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database using a SQL statement.   It returns an IQueryable{DocumentCollection}.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink, "SELECT * FROM colls c WHERE c.id = 'mycoll'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDocumentCollectionQuery(string databaseLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM colls c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "mycoll" }}));
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDocumentCollectionQuery(string databaseLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{StoredProcedure}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{StoredProcedure} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink).Where(c => c.Id == "helloWorld").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<StoredProcedure> CreateStoredProcedureQuery(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink, "SELECT * FROM sprocs s WHERE s.id = 'HelloWorld'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="StoredProcedure"/>
        IQueryable<dynamic> CreateStoredProcedureQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM sprocs s WHERE s.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "HelloWorld" }}));
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="StoredProcedure"/>
        IQueryable<dynamic> CreateStoredProcedureQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{Trigger}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Trigger} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for triggers by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Trigger trigger = client.CreateTriggerQuery(collectionLink).Where(t => t.Id == "validate").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<Trigger> CreateTriggerQuery(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in an Azure Cosmos DB service. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for triggers by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Trigger trigger = client.CreateTriggerQuery(collectionLink, "SELECT * FROM triggers t WHERE t.id = 'validate'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateTriggerQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in the Azure Cosmos DB service using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{Trigger} that can evaluate the query with the provided SQL statement.</returns>
        IQueryable<dynamic> CreateTriggerQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{UserDefinedFunction}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{UserDefinedFunction} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink).Where(u => u.Id == "sqrt").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<UserDefinedFunction> CreateUserDefinedFunctionQuery(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink, "SELECT * FROM udfs u WHERE u.id = 'sqrt'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateUserDefinedFunctionQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in the Azure Cosmos DB service with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM udfs u WHERE u.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "sqrt" }}));
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateUserDefinedFunctionQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{Conflict}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Conflict} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Conflict conflict = client.CreateConflictQuery(collectionLink).Where(c => c.Id == "summary").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<Conflict> CreateConflictQuery(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB service. It returns an IQueryable{Conflict}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM conflicts c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "summary" }}));
        /// Conflict conflict = client.CreateConflictQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Conflict"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateConflictQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB database with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM conflicts c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "summary" }}));
        /// dynamic conflict = client.CreateConflictQuery<dynamic>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateConflictQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        ///  
        /// // Query by the Title property
        /// Book book = client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Title == "War and Peace").AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Author.FirstName == "Leo").AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// foreach (Book matchingBook in client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Price > 100))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// using (var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink,
        ///     new FeedOptions { MaxItemCount = 10 })
        ///     .Where(b => b.Title == "War and Peace")
        ///     .AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started#linq-to-documentdb-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<T> CreateDocumentQuery<T>(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{T}.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent collection.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        /// 
        /// // Query by the Title property
        /// Book book = client.CreateDocumentQuery<Book>(collectionLink, 
        ///     "SELECT * FROM books b WHERE b.title  = 'War and Peace'").AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = client.CreateDocumentQuery<Book>(collectionLink,
        ///     "SELECT * FROM books b WHERE b.Author.FirstName = 'Leo'").AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// foreach (Book matchingBook in client.CreateDocumentQuery<Book>(
        ///     collectionLink, "SELECT * FROM books b where b.Price > 1000"))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// using (var queryable = client.CreateDocumentQuery<Book>(collectionLink, 
        ///     "SELECT * FROM books b WHERE b.title  = 'War and Peace'", 
        ///     new FeedOptions { MaxItemCount = 10 }).AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<T> CreateDocumentQuery<T>(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{T}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        /// 
        /// // Query using Title
        /// Book book, otherBook;
        /// 
        /// var query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        /// book = client.CreateDocumentQuery<Book>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.Author.FirstName = @firstName", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@firstName", Value = "Leo" }}));
        /// otherBook = client.CreateDocumentQuery<Book>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.Price > @minPrice", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@minPrice", Value = 1000 }}));
        /// foreach (Book b in client.CreateDocumentQuery<Book>(
        ///     collectionLink, query))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        ///     
        /// using (var queryable = client.CreateDocumentQuery<Book>(collectionLink, query, 
        ///     new FeedOptions { MaxItemCount = 10 }).AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<T> CreateDocumentQuery<T>(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service. It returns IOrderedQueryable{Document}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Document} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for documents by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Document document = client.CreateDocumentQuery<Document>(collectionLink)
        ///     .Where(d => d.Id == "War and Peace").AsEnumerable().FirstOrDefault();
        ///
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// This overload should be used when the schema of the queried documents is unknown or when querying by ID and replacing/deleting documents.
        /// Since Document is a DynamicObject, it can be dynamically cast back to the original C# object.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<Document> CreateDocumentQuery(string collectionLink, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// // SQL querying allows dynamic property access
        /// dynamic document = client.CreateDocumentQuery<dynamic>(collectionLink,
        ///     "SELECT * FROM books b WHERE b.title == 'War and Peace'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDocumentQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// // SQL querying allows dynamic property access
        /// var query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        ///     
        /// dynamic document = client.CreateDocumentQuery<dynamic>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateDocumentQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a change feed query for documents under a collection in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">Specifies the collection to read documents from.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        /// <remarks>ChangeFeedOptions.PartitionKeyRangeId must be provided.</remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// string partitionKeyRangeId = "0";   // Use client.ReadPartitionKeyRangeFeedAsync() to obtain the ranges.
        /// string checkpointContinuation = null;
        /// ChangeFeedOptions options = new ChangeFeedOptions
        /// {
        ///     PartitionKeyRangeId = partitionKeyRangeId,
        ///     RequestContinuation = checkpointContinuation,
        ///     StartFromBeginning = true,
        /// };
        /// using(var query = client.CreateDocumentChangeFeedQuery(collection.SelfLink, options))
        /// {
        ///     while (true)
        ///     {
        ///         do
        ///         {
        ///             var response = await query.ExecuteNextAsync<Document>();
        ///             if (response.Count > 0)
        ///             {
        ///                 var docs = new List<Document>();
        ///                 docs.AddRange(response);
        ///                 // Process the documents.
        ///                 // Checkpoint response.ResponseContinuation.
        ///             }
        ///         }
        ///         while (query.HasMoreResults);
        ///         Task.Delay(TimeSpan.FromMilliseconds(500)); // Or break here and use checkpointed continuation token later.
        ///     }       
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery{T}"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.ChangeFeedOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyRange"/>
        IDocumentQuery<Document> CreateDocumentChangeFeedQuery(string collectionLink, ChangeFeedOptions feedOptions);

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account. It returns IOrderedQueryable{Offer}.
        /// </summary>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Offer} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the first collection with the S3 offer type
        /// Offer offer = client.CreateOfferQuery().Where(o => o.OfferType == "S3").AsEnumerable().FirstOrDefault();
        /// 
        /// // Find the offer for the collection by SelfLink
        /// Offer offer = client.CreateOfferQuery().Where(o => o.Resource == collectionSelfLink).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IOrderedQueryable<Offer> CreateOfferQuery(FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account using a SQL statement. It returns IQueryable{dynamic}.
        /// </summary>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the first collection with the S3 offer type
        /// Offer offer = client.CreateOfferQuery("SELECT * FROM offers o WHERE o.offerType = 'S3'").AsEnumerable().FirstOrDefault();
        /// 
        /// // Find the offer for the collection by SelfLink
        /// Offer offer = client.CreateOfferQuery(
        ///     string.Format("SELECT * FROM offers o WHERE o.resource = '{0}'", collectionSelfLink)).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateOfferQuery(string sqlExpression, FeedOptions feedOptions = null);

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account using a SQL statement with parameterized values. It returns IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the first collection with the S3 offer type
        /// Offer offer = client.CreateOfferQuery("SELECT * FROM offers o WHERE o.offerType = @offerType",
        /// new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@offerType", Value = "S3" }}))
        /// .AsEnumerable().FirstOrDefault();
        /// 
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        IQueryable<dynamic> CreateOfferQuery(SqlQuerySpec querySpec, FeedOptions feedOptions = null);

        #endregion

    }
}