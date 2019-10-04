//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Security;
    using System.Threading.Tasks;
    using global::Azure.Data.Cosmos;

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
        /// The configured <see cref="Documents.ConsistencyLevel"/> of the client.
        /// </value>
        /// <seealso cref="Documents.ConsistencyLevel"/>
        Documents.ConsistencyLevel ConsistencyLevel { get; }

        #endregion

        #region Account operation

        /// <summary>
        /// Read the <see cref="AccountProperties"/> as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        Task<AccountProperties> GetDatabaseAccountAsync();

        #endregion
    }
}