//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Operations for creating new stored procedures, and reading/querying all stored procedures
    ///
    /// <see cref="CosmosStoredProcedure"/> for reading, replacing, or deleting an existing stored procedures.
    /// </summary>
    public abstract class CosmosStoredProcedures
    {
        /// <summary>
        /// Creates a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The cosmos stored procedure id</param>
        /// <param name="body">The JavaScript function that is the body of the stored procedure</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosStoredProcedureSettings"/> that was created contained within a <see cref="Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="id"/> or <paramref name="body"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosStoredProcedureSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosStoredProcedureSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates and executes a stored procedure that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///    
        /// CosmosStoredProcedure cosmosStoredProcedure = await this.container.StoredProcedures.CreateStoredProceducreAsync(
        ///         id: "appendString",
        ///         body: sprocBody);
        /// 
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await storedProcedure.ExecuteAsync<string, string>(testPartitionId, "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
                    string id,
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the stored procedures for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the stored procedures under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosStoredProcedureSettings> setIterator = this.container.StoredProcedures.GetStoredProcedureIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosStoredProcedureSettings settings in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosResultSetIterator<CosmosStoredProcedureSettings> GetStoredProcedureIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Returns a reference to a stored procedure object. 
        /// </summary>
        /// <param name="id">The cosmos stored procedure id.</param>
        /// <remarks>
        /// Note that the stored procedure must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosStoredProcedure storedProcedure = this.cosmosContainer.StoredProcedures["myStoredProcedureId"];
        /// CosmosStoredProcedureResponse response = await storedProcedure.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosStoredProcedure this[string id] { get; }
    }
}
