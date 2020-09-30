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
    class ChangeFeedRequestOptions : RequestOptions
    {
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

            request.Headers.Add(
                HttpConstants.HttpHeaders.A_IM,
                HttpConstants.A_IMHeaderValues.IncrementalFeed);
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