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
        /// <remarks>This is just a hint to the server which can return less or more items per page. If operations in the container are performed through stored procedures or transactional batch, <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> is preserved when reading items from the Change Feed. As a result, the number of items received could be higher than the specified value so that the items changed by the same transaction are returned as part of one atomic batch.</remarks>
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

        internal ChangeFeedRequestOptions Clone()
        {
            return new ChangeFeedRequestOptions()
            {
                PageSizeHint = this.pageSizeHint,
            };
        }
    }
}