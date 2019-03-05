//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing stored procedures by id.
    /// 
    /// <see cref="CosmosStoredProcedures"/> for creating new stored procedures, and reading/querying all stored procedures;
    /// </summary>
    public abstract class CosmosStoredProcedure : CosmosIdentifier
    {
        /// <summary>
        /// Reads a <see cref="CosmosStoredProcedureSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosStoredProcedureResponse"/> which wraps a <see cref="CosmosStoredProcedureSettings"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
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
        ///  <example>
        ///  This reads an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosStoredProcedureResponse response = await this.cosmosContainer.StoredProcedures["ExistingId"].ReadAsync();
        /// CosmosStoredProcedureSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> ReadAsync(
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="CosmosStoredProcedureSettings"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="body">The JavaScript function to replace the existing resource with.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosStoredProcedureResponse"/> which wraps a <see cref="CosmosStoredProcedureSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="body"/> is not set.</exception>
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
        /// This examples replaces an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated settings
        /// CosmosStoredProcedureSettings settings = new CosmosStoredProcedureSettings
        /// {
        ///     Id = "testTriggerId",
        ///     Body = @"function AddTax() {
        ///         var item = getContext().getRequest().getBody();
        ///
        ///         // Validate/calculate the tax.
        ///         item.tax = item.cost* .15;
        ///
        ///         // Update the request -- this is what is going to be inserted.
        ///         getContext().getRequest().setBody(item);
        ///     }"
        /// };
        /// 
        /// CosmosStoredProcedureResponse response = await this.cosmosStoredProcedure.ReplaceAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> ReplaceAsync(
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosStoredProcedureSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosStoredProcedureResponse"/> which wraps a <see cref="CosmosStoredProcedureSettings"/> which will contain information about the request issued.</returns>
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
        /// This examples gets a reference to an existing stored procedure and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosStoredProcedureResponse response = await this.cosmosContainer.StoredProcedures["taxUdfId"].DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// This examples containers an existing reference to a stored procedure and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosStoredProcedureResponse response = await this.cosmosTaxStoredProcedure.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> DeleteAsync(
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TInput">The input type that is JSON serializable.</typeparam>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="input">The JSON serializable input parameters.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
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
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosItemResponse<TOutput>> ExecuteAsync<TInput, TOutput>(
            object partitionKey,
            TInput input,
            CosmosStoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
