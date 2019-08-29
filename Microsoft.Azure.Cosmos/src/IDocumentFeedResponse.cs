//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// Captures APIs for responses associated with feed methods (enumeration operations) in the Azure Cosmos DB service.
    /// Interface exposed for mocking purposes.
    /// </summary>
    /// <typeparam name="T">The feed type.</typeparam>
    internal interface IDocumentFeedResponse<T>
    {
        /// <summary>
        /// Gets the maximum quota for database resources within the Azure Cosmos DB database account. 
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        long DatabaseQuota { get; }

        /// <summary>
        /// The current number of database resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The number of databases.
        /// </value>
        long DatabaseUsage { get; }

        /// <summary>
        /// Gets the maximum quota for collection resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        long CollectionQuota { get; }

        /// <summary>
        /// The current number of collection resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The number of collections.
        /// </value>
        long CollectionUsage { get; }

        /// <summary>
        /// Gets the maximum quota for user resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        long UserQuota { get; }

        /// <summary>
        /// The current number of user resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The number of users.
        /// </value>
        long UserUsage { get; }

        /// <summary>
        /// Gets the maximum quota for permission resources within the Azure Cosmos DB database account.
        /// </summary>
        /// <value>
        /// The maximum quota for the account.
        /// </value>
        long PermissionQuota { get; }

        /// <summary>
        /// The current number of permission resources within the Azure Cosmos DB database account. 
        /// </summary>
        /// <value>
        /// The number of permissions.
        /// </value>
        long PermissionUsage { get; }

        /// <summary>
        /// Maximum size of a collection in the Azure Cosmos DB database in kilobytes.
        /// </summary>
        /// <value>
        /// Quota in kilobytes.
        /// </value>
        long CollectionSizeQuota { get; }

        /// <summary>
        /// Current size of a collection in the Azure Cosmos DB database in kilobytes. 
        /// </summary>
        /// <vallue>
        /// Current collection size in kilobytes.
        /// </vallue>
        long CollectionSizeUsage { get; }

        /// <summary>
        /// Gets the maximum quota of stored procedures for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        long StoredProceduresQuota { get; }

        /// <summary>
        /// The current number of stored procedures for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of stored procedures.
        /// </value>
        long StoredProceduresUsage { get; }

        /// <summary>
        /// Gets the maximum quota of triggers for a collection in the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The maximum quota.
        /// </value>
        long TriggersQuota { get; }

        /// <summary>
        /// The current number of triggers for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of triggers.
        /// </value>
        long TriggersUsage { get; }

        /// <summary>
        /// Gets the maximum quota of user defined functions for a collection in the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// Maximum quota.
        /// </value>
        long UserDefinedFunctionsQuota { get; }

        /// <summary>
        /// The current number of user defined functions for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Current number of user defined functions.
        /// </value>
        long UserDefinedFunctionsUsage { get; }

        /// <summary>
        /// Gets the number of items returned in the response associated with the feed operations for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Count of items in the response.
        /// </value>
        int Count { get; }

        /// <summary>
        /// Gets the maximum size limit for this entity in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum size limit for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        string MaxResourceQuota { get; }

        /// <summary>
        /// Gets the current size of this entity in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The current size for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        string CurrentResourceQuotaUsage { get; }

        /// <summary>
        /// Gets the request charge for the Azure Cosmos DB database account for this request
        /// </summary>
        /// <value>
        /// The request charge measured in reqest units.
        /// </value>
        double RequestCharge { get; }

        /// <summary>
        /// Gets the activity ID for the request in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        string ActivityId { get; }

        /// <summary>
        /// Gets the continuation token to be used for continuing enumeration in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The continuation token to be used for continuing enumeration.
        /// </value>
        string ResponseContinuation { get; }

        /// <summary>
        /// Gets the session token for use in sesssion consistency reads in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The session token for use in session consistency.
        /// </value>
        string SessionToken { get; }

        /// <summary>
        /// The content parent location in the Azure Cosmos DB database, for example, dbs/foo/colls/bar
        /// </summary>
        string ContentLocation { get; }

        /// <summary>
        /// Gets the response headers.
        /// </summary>
        /// <value>
        /// The response headers.
        /// </value>
        NameValueCollection ResponseHeaders { get; }

        /// <summary>
        /// Returns an enumerator that iterates through a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>An IEnumerator object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator<T> GetEnumerator();
    }
}
