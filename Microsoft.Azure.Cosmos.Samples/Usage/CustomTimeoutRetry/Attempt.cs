namespace Cosmos.Samples.CustomTimeoutRetry
{
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the set of states for a Cosmos DB request relevant to retry + timeout logic
    /// </summary>
    internal enum AttemptStatus
    {
        /// <summary>
        /// Not started, or started and still running (no difference in these states, for our purposes)
        /// </summary>
        Unresolved = 1,
        /// <summary>
        /// Completed successfully within timeout window
        /// </summary>
        Succeeded,
        /// <summary>
        /// Generated an error within timeout window
        /// </summary>
        Failed,
        /// <summary>
        /// Timed out and still cleaning up
        /// </summary>
        TimedOutUnresolved,
        /// <summary>
        /// Ran to completion after timing out
        /// </summary>
        TimedOutSucceeded,
        /// <summary>
        /// Generated an error after timing out
        /// </summary>
        TimedOutFailed,
        /// <summary>
        /// Canceled with no result/error after timing out
        /// </summary>
        TimedOutCanceled
    }

    /// <summary>
    /// encapsulates the result of a Cosmos DB request (states + transitions, end result or exception if any)
    ///  note that due to "cooperative cancellation" behavior inherent in CancellationToken, some timed-out operations may still
    ///  *eventually* produce a value or an error
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    internal class Attempt<TItem>
    {
        public Attempt()
        {
            this.Status = AttemptStatus.Unresolved;
        }

        /// <summary>
        /// This method harvests the results of the original operation passed to it (represented as a Task<Response<typeparamref name="TItem"/>>)
        /// </summary>
        /// <param name="originalTask">Task representing the Cosmos DB request</param>
        /// <param name="diagnosticsFunc">Optional delegate defining diagnostics handling</param>
        public void OnTimeout(Task<Response<TItem>> originalTask, Action<CosmosDiagnostics> diagnosticsFunc)
        {
            if (originalTask.IsFaulted)
            {
                // this might need to handle AggregateException better (iterate all the inner exceptions?)

                Debug.Assert(originalTask.Exception != null);
                Debug.Assert(originalTask.Exception is AggregateException);
                Debug.Assert(originalTask.Exception.InnerException != null);

                if (originalTask.Exception.InnerException is CosmosOperationCanceledException coce)
                {
                    // this is how TaskCanceledException and OperationCanceledException are surfaced in Cosmos DB SDK
                    //  if we get here, it means the original operation canceled with no "harvestable" output

                    this.Result = default;
                    this.Exception = null;
                    this.Status = AttemptStatus.TimedOutCanceled;

                    diagnosticsFunc(coce.Diagnostics);
                }
                else
                {
                    // we eventually got an error for this Cosmos DB operation, let's make it available

                    this.Result = default;
                    this.Exception = originalTask.Exception.InnerException;
                    this.Status = AttemptStatus.TimedOutFailed;

                    if (this.Exception is CosmosException ce)
                    {
                        diagnosticsFunc(ce.Diagnostics);
                    }
                }
            }
            else if (originalTask.IsCompletedSuccessfully)
            {
                // we eventually got real data for this Cosmos DB operation, let's make that available

                this.Result = originalTask.Result;
                this.Exception = default;
                this.Status = AttemptStatus.TimedOutSucceeded;

                diagnosticsFunc(this.Result.Diagnostics);
            }
            else
            {
                // in theory we shouldn't get here, but just in case let's just assume there's nothing for us to harvest

                this.Result = default;
                this.Exception = default;
                this.Status = AttemptStatus.TimedOutCanceled;
            }
        }

        public AttemptStatus Status { get; set; }

        public Response<TItem>? Result { get; set; }

        public Exception? Exception { get; set; }
    }
}
