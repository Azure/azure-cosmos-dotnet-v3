//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Query.Core;

    [DataContract]
    internal sealed class ChangeFeedQuerySpec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.ChangeFeedQuerySpec"/> class for the Azure Cosmos DB service.</summary>
        /// <param name="queryText">The text of the database query.</param>
        /// <param name="enableQueryOnPreviousImage">The boolean when enabaled runs database query on previous image.</param>
        /// <remarks> 
        /// The default constructor initializes any fields to their default values.
        /// </remarks>
        public ChangeFeedQuerySpec(string queryText, bool enableQueryOnPreviousImage)
            : this(queryText)
        {
            this.EnableQueryOnPreviousImage = enableQueryOnPreviousImage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the database query.</param>
        public ChangeFeedQuerySpec(string queryText)
        {
            this.QueryText = queryText ?? throw new ArgumentNullException(nameof(queryText));
        }

        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        private string QueryText { get; set; }

        /// <summary>
        /// Gets or sets if database query should be run on previous image if present.
        /// </summary>
        /// <value>True if query should be run on previous image. False, otherwise.</value>
        [DataMember(Name = "enableQueryOnPreviousImage")]
        private bool EnableQueryOnPreviousImage { get; set; }

        /// <summary>
        /// Returns a value that indicates whether <see cref="QueryText"/> property should be serialized.
        /// </summary>
        internal bool ShouldSerializeQueryText()
        {
            return this.QueryText.Length > 0;
        }

        /// <summary>
        /// Converts to SQL Query Specs
        /// </summary>
        internal SqlQuerySpec ToSqlQuerySpec()
        {
            return new SqlQuerySpec(this.QueryText);
        }
    }
}
