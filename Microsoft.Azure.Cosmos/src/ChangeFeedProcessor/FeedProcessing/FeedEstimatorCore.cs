//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Documents;

    internal sealed class FeedEstimatorCore : FeedEstimator
    {
        private static TimeSpan DefaultMonitoringDelay = TimeSpan.FromSeconds(5);
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();
        private readonly ChangeFeedEstimatorDispatcher dispatcher;
        private readonly RemainingWorkEstimator remainingWorkEstimator;
        private readonly TimeSpan monitoringDelay;

        public FeedEstimatorCore(ChangeFeedEstimatorDispatcher dispatcher, RemainingWorkEstimator remainingWorkEstimator)
        {
            this.dispatcher = dispatcher;
            this.remainingWorkEstimator = remainingWorkEstimator;
            this.monitoringDelay = dispatcher.DispatchPeriod ?? FeedEstimatorCore.DefaultMonitoringDelay;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = this.monitoringDelay;

                try
                {
                    long estimation = await this.remainingWorkEstimator.GetEstimatedRemainingWorkAsync(cancellationToken).ConfigureAwait(false);
                    await this.dispatcher.DispatchEstimation(estimation, cancellationToken);
                }
                catch (DocumentClientException clientException)
                {
                    this.logger.WarnException("exception within estimator", clientException);
                    DocDbError docDbError = ExceptionClassifier.ClassifyClientException(clientException);
                    switch (docDbError)
                    {
                        case DocDbError.Undefined:
                            throw;
                        case DocDbError.PartitionNotFound:
                        case DocDbError.PartitionSplit:
                        case DocDbError.TransientError:
                            // Retry on transient (429) errors
                            break;
                        default:
                            this.logger.Fatal($"Unrecognized DocDbError enum value {docDbError}");
                            Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                            throw;
                    }

                    if (clientException.RetryAfter != TimeSpan.Zero)
                        delay = clientException.RetryAfter;
                }
                catch (TaskCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    this.logger.WarnException("exception within estimator", canceledException);

                    // ignore as it is caused by DocumentDB client
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}