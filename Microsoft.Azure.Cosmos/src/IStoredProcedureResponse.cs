//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Specialized;
    using System.Net;
    using Microsoft.Azure.Cosmos.Scripts;

    /// <summary>
    /// Interface exposed for mocking purposes for the Azure Cosmos DB service.
    /// </summary>
    /// <typeparam name="TValue">The returned value type of the stored procedure.</typeparam>
    internal interface IStoredProcedureResponse<TValue>
    {
        /// <summary>
        /// Gets the Activity ID of the request.
        /// </summary>
        /// <value>
        /// The Activity ID of the request.
        /// </value>
        /// <remarks>Every request is traced with a globally unique ID. 
        /// Include activity ID in tracing application failures and when contacting Azure Cosmos DB support.
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string ActivityId { get; }

        /// <summary>
        /// Gets the delimited string containing the usage of each resource type within the collection.
        /// </summary>
        /// <value>The delimited string containing the number of used units per resource type within the collection.</value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string CurrentResourceQuotaUsage { get; }

        /// <summary>
        /// Gets the delimited string containing the quota of each resource type within the collection.
        /// </summary>
        /// <value>The delimited string containing the number of used units per resource type within the collection.</value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string MaxResourceQuota { get; }

        /// <summary>
        /// Gets the number of normalized request units (RUs) charged.
        /// </summary>
        /// <value>
        /// The number of normalized request units (RUs) charged.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        double RequestCharge { get; }

        /// <summary>
        /// Gets the response of a stored procedure, serialized into the given type.
        /// </summary>
        /// <value>The response of a stored procedure, serialized into the given type.</value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        TValue Response { get; }

        /// <summary>
        /// Gets the headers associated with the response.
        /// </summary>
        /// <value>
        /// Headers associated with the response.
        /// </value>
        /// <remarks>
        /// Provides access to all HTTP response headers returned from the 
        /// Azure Cosmos DB API.
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        NameValueCollection ResponseHeaders { get; }

        /// <summary>
        /// Gets the token for use with session consistency requests.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        string SessionToken { get; }

        /// <summary>
        /// Gets the output from stored procedure console.log() statements.
        /// </summary>
        /// <value>
        /// Output from console.log() statements in a stored procedure.
        /// </value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        /// <seealso cref="StoredProcedureRequestOptions.EnableScriptLogging"/>
        string ScriptLog { get; }

        /// <summary>
        /// Gets the request completion status code.
        /// </summary>
        /// <value>The request completion status code</value>
        /// <remarks>
        /// This is exposed for mocking purposes for the Azure Cosmos DB service.
        /// </remarks>
        HttpStatusCode StatusCode { get; }
    }
}