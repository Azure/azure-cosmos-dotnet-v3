//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class ChangeFeedRequestOptions : RequestOptions
    {
        private static readonly ChangeFeedMode DefaultMode = ChangeFeedMode.Incremental();

        private int? pageSizeHint;

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value>
        /// <remarks>This is just a hint to the server which can return less items per page.</remarks>
        public int? PageSizeHint
        {
            get => this.pageSizeHint;
            set
            {
                if (value.HasValue && (value.Value <= 0))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(this.PageSizeHint)} must be a positive value.");
                }

                this.pageSizeHint = value;
            }
        }

        /// <summary>
        /// Gets or sets whether or not to emit the old continuation token.
        /// </summary>
        /// <remarks>
        /// This is useful for when you want to upgrade your SDK, but can not upgrade all nodes atomically. 
        /// In that scenario perform a "two phase deployment".
        /// In the first phase deploy with EmitOldContinuationToken = true; this will ensure that none of the old SDKs encounter a new continuation token.
        /// Once the first phase is complete and all nodes are able to read the new continuation token, then
        /// in the second phase redeploy with EmitOldContinuationToken = false.
        /// This will succesfully migrate all nodes to emit the new continuation token format.
        /// Eventually all your tokens from the previous version will be migrated. 
        /// </remarks>
        public bool EmitOldContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the desired mode on which to consume the Change Feed.
        /// </summary>
        /// <value>Default value is <see cref="ChangeFeedMode.Incremental"/>. </value>
        public ChangeFeedMode FeedMode { get; set; } = ChangeFeedRequestOptions.DefaultMode;

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            Debug.Assert(request != null);

            base.PopulateRequestOptions(request);

            if (this.PageSizeHint.HasValue)
            {
                request.Headers.Add(
                    HttpConstants.HttpHeaders.PageSize,
                    this.PageSizeHint.Value.ToString(CultureInfo.InvariantCulture));
            }

            this.FeedMode.Accept(request);
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

        internal ChangeFeedRequestOptions Clone()
        {
            return new ChangeFeedRequestOptions()
            {
                PageSizeHint = this.pageSizeHint,
            };
        }
    }
}