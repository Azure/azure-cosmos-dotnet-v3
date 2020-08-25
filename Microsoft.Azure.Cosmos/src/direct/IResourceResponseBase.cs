//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Client
{
    using Microsoft.Azure.Documents.Collections;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;

    /// <summary>
    /// Represents the non-resource specific service response headers returned by any request in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// Interface exposed for mocking purposes for the Azure Cosmos DB service.
    /// </remarks>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    interface IResourceResponseBase
    {
        /// <summary>
        /// Gets the maximum quota for database resources within the account. 
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long DatabaseQuota { get; }

        /// <summary>
        /// The current number of database resources within the account.
        /// </summary>
        /// <value>
        /// The number of databases.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long DatabaseUsage { get; }

        /// <summary>
        /// Gets the maximum quota for collection resources within an account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long CollectionQuota { get; }

        /// <summary>
        /// The current number of collection resources within the account.
        /// </summary>
        /// <value>
        /// The number of collections.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long CollectionUsage { get; }

        /// <summary>
        /// Gets the maximum quota for user resources within an account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long UserQuota { get; }

        /// <summary>
        /// The current number of user resources within the account.
        /// </summary>
        /// <value>
        /// The number of users.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long UserUsage { get; }

        /// <summary>
        /// Gets the maximum quota for permission resources within an account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long PermissionQuota { get; }

        /// <summary>
        /// The current number of permission resources within the account. 
        /// </summary>
        /// <value>
        /// The number of permissions.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long PermissionUsage { get; }

        /// <summary>
        /// Maximum size of a collection in kilobytes.
        /// </summary>
        /// <value>
        /// Quota in kilobytes.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long CollectionSizeQuota { get; }

        /// <summary>
        /// Current size of a collection in kilobytes. 
        /// </summary>
        /// <value>
        /// Current collection size in kilobytes.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long CollectionSizeUsage { get; }

        /// <summary>
        /// Maximum size of a documents within a collection in kilobytes.
        /// </summary>
        /// <value>
        /// Quota in kilobytes.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long DocumentQuota { get; }

        /// <summary>
        /// Current size of documents within a collection in kilobytes. 
        /// </summary>
        /// <value>
        /// Current documents size in kilobytes.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long DocumentUsage { get; }

        /// <summary>
        /// Gets the maximum quota of stored procedures for a collection.
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long StoredProceduresQuota { get; }

        /// <summary>
        /// The current number of stored procedures for a collection.
        /// </summary>
        /// <value>
        /// Current number of stored procedures.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long StoredProceduresUsage { get; }

        /// <summary>
        /// Gets the maximum quota of triggers for a collection. 
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long TriggersQuota { get; }

        /// <summary>
        /// The current number of triggers for a collection.
        /// </summary>
        /// <value>
        /// Current number of triggers.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long TriggersUsage { get; }

        /// <summary>
        /// Gets the maximum quota of user defined functions for a collection. 
        /// </summary>
        /// <value>
        /// Maximum quota.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long UserDefinedFunctionsQuota { get; }

        /// <summary>
        /// The current number of user defined functions for a collection.
        /// </summary>
        /// <value>
        /// Current number of user defined functions.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long UserDefinedFunctionsUsage { get; }

        /// <summary>
        /// Gets the activity ID for the request.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string ActivityId { get; }

        /// <summary>
        /// Gets the session token for use in sesssion consistency reads.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string SessionToken { get; }

        /// <summary>
        /// Gets the HTTP status code associated with the response.
        /// </summary>
        /// <value>
        /// The HTTP status code associated with the response.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the maximum size limit for this entity.
        /// </summary>
        /// <value>
        /// The maximum size limit for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string MaxResourceQuota { get; }

        /// <summary>
        /// Gets the current size of this entity.
        /// </summary>
        /// <value>
        /// The current size for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string CurrentResourceQuotaUsage { get; }

        /// <summary>
        /// Gets the underlying stream of the response.
        /// </summary>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        Stream ResponseStream { get; }

        /// <summary>
        /// Gets the request charge for this request.
        /// </summary>
        /// <value>
        /// The request charge measured in reqest units.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        double RequestCharge { get; }

        /// <summary>
        /// Gets the response headers.
        /// </summary>
        /// <value>
        /// The response headers.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        NameValueCollection ResponseHeaders { get; }

        /// <summary>
        /// The content parent location, for example, dbs/foo/colls/bar
        /// </summary>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string ContentLocation { get; }

        /// <summary>
        /// Gets the progress of an index transformation, if one is underway.
        /// </summary>
        /// <value>
        /// An integer from 0 to 100 representing percentage completion of the index transformation process.
        /// Returns -1 if the index transformation progress header could not be found.
        /// </value>
        /// <remarks>
        /// An index will be rebuilt when the IndexPolicy of a collection is updated.
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long IndexTransformationProgress { get; }

        /// <summary>
        /// Gets the progress of lazy indexing.
        /// </summary>
        /// <value>
        /// An integer from 0 to 100 representing percentage completion of the lazy indexing process.
        /// Returns -1 if the lazy indexing progress header could not be found.
        /// </value>
        /// <remarks>
        /// Lazy indexing progress only applies to the collection with indexing mode Lazy.
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        long LazyIndexingProgress { get; }
    }
}