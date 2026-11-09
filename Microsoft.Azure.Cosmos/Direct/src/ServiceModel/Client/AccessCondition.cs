//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Client
{
    /// <summary>
    /// Represents a set of access conditions to be used for operations in the Azure Cosmos DB service.
    /// </summary>
    /// <example>
    /// The following example shows how to use AccessCondition with DocumentClient.
    /// <code language="c#">
    /// <![CDATA[
    /// // If ETag is current, then this will succeed. Otherwise the request will fail with HTTP 412 Precondition Failure
    /// await client.ReplaceDocumentAsync(
    ///     readCopyOfBook.SelfLink, 
    ///     new Book { Title = "Moby Dick", Price = 14.99 },
    ///     new RequestOptions 
    ///     { 
    ///         AccessCondition = new AccessCondition 
    ///         { 
    ///             Condition = readCopyOfBook.ETag, 
    ///             Type = AccessConditionType.IfMatch 
    ///         } 
    ///      });
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="AccessConditionType"/>
    /// <seealso cref="RequestOptions"/>
    /// <seealso cref="Resource"/>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class AccessCondition
    {
        /// <summary>
        /// Gets or sets the condition type in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The condition type. Can be IfMatch or IfNoneMatch.
        /// </value>
        public AccessConditionType Type { get; set; }

        /// <summary>
        /// Gets or sets the value of the condition in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The value of the condition. For <see cref="AccessConditionType"/> IfMatch and IfNotMatch, this is the ETag that has to be compared to.
        /// </value>
        /// <seealso cref="Resource"/>
        public string Condition { get; set; }
    }
}
