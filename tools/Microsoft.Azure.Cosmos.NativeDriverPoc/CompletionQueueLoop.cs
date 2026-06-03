// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// V2 receive loop for the production
    /// <c>azure_data_cosmos_driver_native</c> completion queue
    /// (header lines 696-835). One background thread per CQ calls
    /// <see cref="NativeMethods.cosmos_cq_wait"/>, retrieves an opaque
    /// <c>cosmos_completion_t*</c>, fans the outcome onto the matching
    /// <see cref="TaskCompletionSource{T}"/>, then frees the completion.
    /// </summary>
    internal sealed class CompletionQueueLoop : IDisposable
    {
        private readonly IntPtr cqHandle;
        private readonly Thread pumpThread;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<CosmosNativeResponse>> pending;
        private long nextUserData;
        private int disposed;

        public CompletionQueueLoop(IntPtr runtime, CosmosCqOptions? options = null)
        {
            this.cqHandle = options.HasValue
                ? cosmos_cq_create(runtime, options.Value)
                : cosmos_cq_create_default(runtime, IntPtr.Zero);
            if (this.cqHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("cosmos_cq_create returned NULL");
            }

            this.pending = new ConcurrentDictionary<ulong, TaskCompletionSource<CosmosNativeResponse>>();
            this.pumpThread = new Thread(this.Pump)
            {
                IsBackground = true,
                Name = "cosmos-native-driver-cq-pump",
            };
            this.pumpThread.Start();
        }

        public IntPtr Handle => this.cqHandle;

        public IntPtr Register(TaskCompletionSource<CosmosNativeResponse> tcs, out ulong token)
        {
            token = (ulong)Interlocked.Increment(ref this.nextUserData);
            this.pending[token] = tcs;
            return new IntPtr((long)token);
        }

        public bool Unregister(ulong token) => this.pending.TryRemove(token, out _);

        private void Pump()
        {
            while (Volatile.Read(ref this.disposed) == 0)
            {
                IntPtr completion = cosmos_cq_wait(this.cqHandle, timeoutMs: 200);
                if (completion == IntPtr.Zero)
                {
                    // Header §720 — NULL on timeout / shutdown / drained / spurious wake.
                    CosmosCqState state = cosmos_cq_state(this.cqHandle);
                    if (state == CosmosCqState.Running)
                    {
                        continue;
                    }
                    this.DrainPendingOnShutdown();
                    return;
                }

                try
                {
                    this.DispatchCompletion(completion);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[pump] dispatch error: {ex}");
                }
                finally
                {
                    cosmos_completion_free(completion);
                }
            }

            cosmos_cq_shutdown(this.cqHandle);
            this.DrainPendingOnShutdown();
        }

        private void DispatchCompletion(IntPtr completion)
        {
            IntPtr userDataPtr = cosmos_completion_user_data(completion);
            ulong token = (ulong)userDataPtr.ToInt64();

            if (!this.pending.TryRemove(token, out var tcs))
            {
                Console.Error.WriteLine(
                    $"[pump] completion for unknown user_data 0x{token:X16} dropped");
                return;
            }

            CosmosCompletionOutcome outcome = cosmos_completion_outcome(completion);

            switch (outcome)
            {
                case CosmosCompletionOutcome.Ok:
                {
                    IntPtr response = cosmos_completion_take_response(completion);
                    try
                    {
                        tcs.TrySetResult(MaterializeResponse(response));
                    }
                    finally
                    {
                        if (response != IntPtr.Zero)
                        {
                            cosmos_response_free(response);
                        }
                    }
                    break;
                }

                case CosmosCompletionOutcome.Error:
                {
                    CosmosErrorCode coarse = cosmos_completion_status(completion);
                    IntPtr error = cosmos_completion_take_error(completion);
                    Exception ex;
                    try
                    {
                        ex = new CosmosNativeException(error, coarse);
                    }
                    finally
                    {
                        if (error != IntPtr.Zero) cosmos_error_free(error);
                    }
                    tcs.TrySetException(ex);
                    break;
                }

                case CosmosCompletionOutcome.Cancelled:
                {
                    tcs.TrySetCanceled();
                    break;
                }

                default:
                {
                    tcs.TrySetException(new InvalidOperationException(
                        $"unexpected cosmos_completion_outcome: {outcome}"));
                    break;
                }
            }
        }

        private static CosmosNativeResponse MaterializeResponse(IntPtr response)
        {
            if (response == IntPtr.Zero)
            {
                return new CosmosNativeResponse(0, 0.0, null, null, null, null, Array.Empty<byte>());
            }

            ushort http = cosmos_response_status_code(response);
            double ru = cosmos_response_request_charge(response);
            string? activityId = PtrToUtf8(cosmos_response_activity_id(response));
            string? sessionToken = PtrToUtf8(cosmos_response_session_token(response));
            string? etag = PtrToUtf8(cosmos_response_etag(response));
            string? continuation = PtrToUtf8(cosmos_response_continuation_token(response));

            byte[] body = Array.Empty<byte>();
            if (cosmos_response_body(response, out IntPtr dataPtr, out UIntPtr lenNative) == CosmosErrorCode.Success
                && dataPtr != IntPtr.Zero)
            {
                int len = (int)lenNative.ToUInt32();
                if (len > 0)
                {
                    body = new byte[len];
                    Marshal.Copy(dataPtr, body, 0, len);
                }
            }

            return new CosmosNativeResponse(http, ru, activityId, sessionToken, etag, continuation, body);
        }

        private void DrainPendingOnShutdown()
        {
            foreach (var kvp in this.pending)
            {
                kvp.Value.TrySetException(
                    new InvalidOperationException("Completion queue was shut down or drained"));
            }
            this.pending.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }
            this.pumpThread.Join(TimeSpan.FromSeconds(5));
            cosmos_cq_free(this.cqHandle);
        }
    }
}
