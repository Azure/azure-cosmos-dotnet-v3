//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a stored procedure in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// Azure Cosmos DB allows application logic written entirely in JavaScript to be executed directly inside the database engine under the database transaction.
    /// For additional details, refer to the server-side JavaScript API documentation.
    /// </remarks>
    public class CosmosStoredProcedureSettings : CosmosResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.StoredProcedure"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosStoredProcedureSettings()
        {
        }

        /// <summary>
        /// Gets or sets the body of the Azure Cosmos DB stored procedure.
        /// </summary>
        /// <value>The body of the stored procedure.</value>
        /// <remarks>Must be a valid JavaScript function. For e.g. "function () { getContext().getResponse().setBody('Hello World!'); }"</remarks>
        [JsonProperty(PropertyName = Constants.Properties.Body)]
        public string Body { get; set; }
    }
}
