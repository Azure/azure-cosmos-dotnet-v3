//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DisposalWitness
    {
        private readonly TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> journal;
        private readonly string id;
        private readonly Action onCompleted;

        public DisposalWitness(string id, ConcurrentQueue<string> journal, Action onCompleted = null)
        {
            this.id = id;
            this.journal = journal;
            this.onCompleted = onCompleted;
        }

        public Task Completed => this.completed.Task;

        public bool IsCompleted => this.completed.Task.IsCompletedSuccessfully;

        public void RequireCompleted()
        {
            if (!this.IsCompleted)
            {
                throw new InvalidOperationException("Previous content disposal has not completed before dispatch.");
            }
        }

        internal void Complete()
        {
            this.journal.Enqueue("Disposed:" + this.id);
            this.completed.SetResult(true);
            this.onCompleted?.Invoke();
        }
    }

    internal sealed class DisposalObservedContent : HttpContent
    {
        private readonly HttpContent inner;
        private readonly DisposalWitness witness;
        private int disposalStarted;
        private int readCalls;

        public DisposalObservedContent(HttpContent inner, DisposalWitness witness)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.witness = witness ?? throw new ArgumentNullException(nameof(witness));

            foreach (KeyValuePair<string, IEnumerable<string>> header in inner.Headers)
            {
                if (!this.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    throw new InvalidOperationException("Could not preserve a content header.");
                }
            }
        }

        public int ReadCalls => Volatile.Read(ref this.readCalls);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.readCalls);
            return this.inner.ReadAsStream(cancellationToken);
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            Interlocked.Increment(ref this.readCalls);
            return this.inner.ReadAsStreamAsync();
        }

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.readCalls);
            return this.inner.ReadAsStreamAsync(cancellationToken);
        }

        protected override void SerializeToStream(
            Stream stream,
            TransportContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.readCalls);
            this.inner.CopyTo(stream, context, cancellationToken);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            Interlocked.Increment(ref this.readCalls);
            return this.inner.CopyToAsync(stream, context);
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.readCalls);
            return this.inner.CopyToAsync(stream, context, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                base.Dispose(false);
                return;
            }

            if (Interlocked.Exchange(ref this.disposalStarted, 1) != 0)
            {
                return;
            }

            this.inner.Dispose();
            base.Dispose(true);
            this.witness.Complete();
        }
    }
}