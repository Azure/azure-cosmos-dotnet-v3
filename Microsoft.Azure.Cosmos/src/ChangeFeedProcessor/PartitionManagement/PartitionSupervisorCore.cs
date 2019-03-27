//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Utils;

    internal sealed class PartitionSupervisorCore<T> : PartitionSupervisor
    {
        private readonly DocumentServiceLease lease;
        private readonly ChangeFeedObserver<T> observer;
        private readonly PartitionProcessor processor;
        private readonly LeaseRenewer renewer;
        private readonly CancellationTokenSource renewerCancellation = new CancellationTokenSource();
        private CancellationTokenSource processorCancellation;

        public PartitionSupervisorCore(DocumentServiceLease lease, ChangeFeedObserver<T> observer, PartitionProcessor processor, LeaseRenewer renewer)
        {
            this.lease = lease;
            this.observer = observer;
            this.processor = processor;
            this.renewer = renewer;
        }

        public override async Task RunAsync(CancellationToken shutdownToken)
        {
            var context = new ChangeFeedObserverContextCore<T>(this.lease.CurrentLeaseToken);
            await this.observer.OpenAsync(context).ConfigureAwait(false);

            this.processorCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);

            Task processorTask = this.processor.RunAsync(this.processorCancellation.Token);
            processorTask.ContinueWith(_ => this.renewerCancellation.Cancel()).LogException();

            Task renewerTask = this.renewer.RunAsync(this.renewerCancellation.Token);
            renewerTask.ContinueWith(_ => this.processorCancellation.Cancel()).LogException();

            ChangeFeedObserverCloseReason closeReason = shutdownToken.IsCancellationRequested ?
                ChangeFeedObserverCloseReason.Shutdown :
                ChangeFeedObserverCloseReason.Unknown;

            try
            {
                await Task.WhenAll(processorTask, renewerTask).ConfigureAwait(false);
            }
            catch (LeaseLostException)
            {
                closeReason = ChangeFeedObserverCloseReason.LeaseLost;
                throw;
            }
            catch (PartitionSplitException)
            {
                closeReason = ChangeFeedObserverCloseReason.LeaseGone;
                throw;
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                closeReason = ChangeFeedObserverCloseReason.Shutdown;
            }
            catch (ObserverException)
            {
                closeReason = ChangeFeedObserverCloseReason.ObserverError;
                throw;
            }
            catch (Exception) when (processorTask.IsFaulted)
            {
                closeReason = ChangeFeedObserverCloseReason.Unknown;
                throw;
            }
            finally
            {
                await this.observer.CloseAsync(context, closeReason).ConfigureAwait(false);
            }
        }

        public override void Dispose()
        {
            this.processorCancellation?.Dispose();
            this.renewerCancellation.Dispose();
        }
    }
}