//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Serializer;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
    public sealed class ChangeFeedRequestOptions : RequestOptions
    {
        private int? pageSizeHint;

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value>
        /// <remarks>
        /// <para>This is just a hint to the server which can return less or more items per page. If operations in the container are performed through stored procedures or transactional batch, <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> is preserved when reading items from the Change Feed. As a result, the number of items received could be higher than the specified value so that the items changed by the same transaction are returned as part of one atomic batch.</para>
        /// <para>
        /// Set this value to zero only when <see cref="EnableContinuationTokenOnly"/> is enabled;
        /// otherwise, it must be positive or unspecified. The combination is validated when
        /// creating the iterator, so the properties can be assigned in either order.
        /// Negative values are rejected when assigned.
        /// </para>
        /// </remarks>
        public int? PageSizeHint
        {
            get => this.pageSizeHint;
            set
            {
                if (value.HasValue && (value.Value < 0))
                {
                    throw new ArgumentOutOfRangeException(nameof(this.PageSizeHint), value, "The page size must not be negative.");
                }

                this.pageSizeHint = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to acquire a continuation token without returning documents.
        /// The default is <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This option is supported only with <see cref="ChangeFeedStartFrom.Now()"/> and works with
        /// <see cref="ChangeFeedMode.LatestVersion"/> and <see cref="ChangeFeedMode.AllVersionsAndDeletes"/>.
        /// Other starting positions cause an <see cref="ArgumentException"/> when creating the iterator.
        /// <see cref="PageSizeHint"/> must be explicitly set to zero. An unspecified or nonzero
        /// value causes an <see cref="ArgumentOutOfRangeException"/> when creating the iterator.
        /// </para>
        /// <para>
        /// One successful <see cref="FeedIterator.ReadNextAsync"/> call visits all ranges in the
        /// iterator's scope and returns a composite continuation token, no documents, and the total
        /// request charge. The response retains the final service status (200 or 304).
        /// The token represents each range when visited, not an atomic cross-partition snapshot.
        /// </para>
        /// <para>
        /// <see cref="FeedIterator.HasMoreResults"/> starts as <see langword="true"/> and becomes
        /// <see langword="false"/> after a complete cycle. Explicit subsequent reads are allowed and
        /// perform another token-only cycle from the current state. If a range fails mid-cycle,
        /// the last successful response and partial continuation are returned first; the buffered
        /// error is surfaced on the next explicit read. An interrupted cycle does not mark the
        /// iterator complete. Resume normal reading with a new iterator, the same mode, and this
        /// option disabled.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// using FeedIterator capture = container.GetChangeFeedStreamIterator(
        ///     ChangeFeedStartFrom.Now(),
        ///     ChangeFeedMode.LatestVersion,
        ///     new ChangeFeedRequestOptions { EnableContinuationTokenOnly = true, PageSizeHint = 0 });
        /// using ResponseMessage response = await capture.ReadNextAsync();
        /// // A complete cycle has no more results; an interrupted cycle requires another read.
        /// if (!capture.HasMoreResults &amp;&amp;
        ///     (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotModified))
        /// {
        ///     using FeedIterator reader = container.GetChangeFeedStreamIterator(
        ///         ChangeFeedStartFrom.ContinuationToken(response.Headers.ContinuationToken),
        ///         ChangeFeedMode.LatestVersion);
        /// }
        /// </code>
        /// </example>
        public bool EnableContinuationTokenOnly { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ReadConsistencyStrategy"/> for the request.
        /// </summary>
        /// <remarks>
        /// When set, this takes precedence over the account's default consistency level for change feed operations.
        /// The <see cref="ReadConsistencyStrategy.GlobalStrong"/> strategy is only valid
        /// for accounts configured with Strong consistency.
        /// </remarks>
        public ReadConsistencyStrategy? ReadConsistencyStrategy
        {
            get => this.BaseReadConsistencyStrategy;
            set => this.BaseReadConsistencyStrategy = value;
        }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            Debug.Assert(request != null);

            base.PopulateRequestOptions(request);
        }

        /// <summary>
        /// IfMatchEtag is inherited from the base class but not used. 
        /// </summary>
        [Obsolete("IfMatchEtag is inherited from the base class but not used.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new string IfMatchEtag
        {
            get => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
            set => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
        }

        /// <summary>
        /// IfNoneMatchEtag is inherited from the base class but not used. 
        /// </summary>
        [Obsolete("IfNoneMatchEtag is inherited from the base class but not used.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new string IfNoneMatchEtag
        {
            get => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfNoneMatchEtag)} property.");
            set => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfNoneMatchEtag)} property.");
        }

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
        public
#else
        internal
#endif
        JsonSerializationFormatOptions JsonSerializationFormatOptions { get; set; }

        /// <summary>
        /// Enables printing query in Traces db.query.text attribute. By default, query is not printed.
        /// Users have the option to enable printing parameterized or all queries, 
        /// but has to beware that customer data may be shown when the later option is chosen. It's the user's responsibility to sanitize the queries if necessary.
        /// </summary>
        internal QueryTextMode QueryTextMode { get; set; } = Cosmos.QueryTextMode.None;

        internal static void ValidatePageSizeHint(int? pageSizeHint, bool enableContinuationTokenOnly)
        {
            if (enableContinuationTokenOnly && pageSizeHint != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(PageSizeHint),
                    pageSizeHint,
                    $"{nameof(PageSizeHint)} must be explicitly set to zero when {nameof(EnableContinuationTokenOnly)} is enabled.");
            }

            if (!enableContinuationTokenOnly && pageSizeHint.HasValue && pageSizeHint.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(PageSizeHint),
                    pageSizeHint,
                    $"{nameof(PageSizeHint)} must be positive when {nameof(EnableContinuationTokenOnly)} is disabled.");
            }
        }

        internal ChangeFeedRequestOptions Clone()
        {
            return new ChangeFeedRequestOptions()
            {
                PageSizeHint = this.pageSizeHint,
                EnableContinuationTokenOnly = this.EnableContinuationTokenOnly,
            };
        }
    }
}