//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    internal sealed class PartitionSupervisorCore<T> : PartitionSupervisor
    {
        private readonly DocumentServiceLease lease;
        private readonly ChangeFeedObserver<T> observer;
        private readonly FeedProcessor processor;
        private readonly LeaseRenewer renewer;
        private readonly CancellationTokenSource renewerCancellation = new CancellationTokenSource();
        private CancellationTokenSource processorCancellation;

        public PartitionSupervisorCore(DocumentServiceLease lease, ChangeFeedObserver<T> observer, FeedProcessor processor, LeaseRenewer renewer)
        {
            this.lease = lease;
            this.observer = observer;
            this.processor = processor;
            this.renewer = renewer;
        }

        public override async Task RunAsync(CancellationToken shutdownToken)
        {
            ChangeFeedObserverContextCore<T> context = new ChangeFeedObserverContextCore<T>(this.lease.CurrentLeaseToken);
            await this.observer.OpenAsync(context).ConfigureAwait(false);

            this.processorCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);

            Task processorTask = this.processor.RunAsync(this.processorCancellation.Token);
            Task renewerTask = this.renewer.RunAsync(this.renewerCancellation.Token);

            ChangeFeedObserverCloseReason closeReason = ChangeFeedObserverCloseReason.Unknown;
            Task task;
            try
            {
                task = await Task.WhenAny(processorTask, renewerTask).ConfigureAwait(false);

                try
                {
                    (task == processorTask ? this.renewerCancellation : this.processorCancellation).Cancel();
                }
                catch (Exception ex)
                {
                    Extensions.TraceException(ex);
                }

                (task == processorTask ? renewerTask : processorTask).LogException();

                if (shutdownToken.IsCancellationRequested) closeReason = ChangeFeedObserverCloseReason.Shutdown;

                if (task.IsFaulted)
                {
                    closeReason = task.Exception.InnerException switch
                    {
                        LeaseLostException _ => ChangeFeedObserverCloseReason.LeaseLost,
                        FeedSplitException _ => ChangeFeedObserverCloseReason.LeaseGone,
                        FeedNotFoundException _ => ChangeFeedObserverCloseReason.ResourceGone,
                        FeedReadSessionNotAvailableException _ => ChangeFeedObserverCloseReason.ReadSessionNotAvailable,
                        ObserverException _ => ChangeFeedObserverCloseReason.ObserverError,
                        _ => closeReason
                    };
                }
            }
            finally
            {
                await this.observer.CloseAsync(context, closeReason).ConfigureAwait(false);
            }
            task.GetAwaiter().GetResult(); // re-throw exceptions/cancellations to set the result for this task
        }

        public override void Dispose()
        {
            this.processorCancellation?.Dispose();
            this.renewerCancellation.Dispose();
        }
    }
}