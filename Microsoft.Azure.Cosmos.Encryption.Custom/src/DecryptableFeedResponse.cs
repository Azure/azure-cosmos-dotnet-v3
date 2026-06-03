//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DecryptableFeedResponse<T> : FeedResponse<T>, IAsyncDisposable
    {
        private int isDisposed;

        protected DecryptableFeedResponse(
            ResponseMessage responseMessage,
            IReadOnlyCollection<T> resource)
        {
            this.Count = resource?.Count ?? 0;
            this.Headers = responseMessage.Headers;
            this.StatusCode = responseMessage.StatusCode;
            this.Diagnostics = responseMessage.Diagnostics;
            this.Resource = resource;
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        // TODO: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2750
        public override string IndexMetrics => null;

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static DecryptableFeedResponse<T> CreateResponse(
            ResponseMessage responseMessage,
            IReadOnlyCollection<T> resource)
        {
            ArgumentValidation.ThrowIfNull(responseMessage);

            using (responseMessage)
            {
                // ReadFeed can return 304 on Change Feed responses
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                return new DecryptableFeedResponse<T>(
                    responseMessage,
                    resource);
            }
        }

        /// <summary>
        /// Cascades asynchronous disposal to any <see cref="IAsyncDisposable"/> items in <see cref="Resource"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Stream-mode <see cref="DecryptableItem"/> instances wrap pooled buffers that must be returned
        /// to <see cref="System.Buffers.ArrayPool{T}"/> to prevent buffer leaks and to clear any plaintext
        /// residue. Callers that abandon iteration (early-exit, exception, or never enumerate) rely on this
        /// cascade to release those buffers. The cascade is idempotent: items already disposed by
        /// <see cref="DecryptableItem.GetItemAsync{T}"/> are no-ops on subsequent disposal calls.
        /// Items that do not implement <see cref="IAsyncDisposable"/> are skipped.
        /// </para>
        /// <para>
        /// Disposal is best-effort across the page: if a single item throws from
        /// <see cref="IAsyncDisposable.DisposeAsync"/>, the cascade continues through the remainder of the
        /// page before propagating. A single failure is rethrown as-is; multiple failures are aggregated
        /// into an <see cref="AggregateException"/>. This guarantees that a misbehaving item cannot strand
        /// the rented buffers of its peers.
        /// </para>
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.isDisposed, 1) != 0)
            {
                return;
            }

            List<Exception> failures = null;

            if (this.Resource != null)
            {
                foreach (T item in this.Resource)
                {
                    if (item is IAsyncDisposable asyncDisposable)
                    {
                        try
                        {
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            (failures ??= new List<Exception>()).Add(ex);
                        }
                    }
                }
            }

            GC.SuppressFinalize(this);

            if (failures != null)
            {
                if (failures.Count == 1)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
                }

                throw new AggregateException(failures);
            }
        }
    }
}