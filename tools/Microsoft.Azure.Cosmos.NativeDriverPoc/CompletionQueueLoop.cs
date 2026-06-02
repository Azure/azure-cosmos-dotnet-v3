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
    /// V2 receive loop for the production-spec
    /// <c>azure_data_cosmos_driver_native</c> completion queue
    /// (NATIVE_WRAPPER_SPEC.md §3.1). One dedicated background thread per CQ
    /// calls <see cref="NativeMethods.cosmos_cq_wait"/>, retrieves a
    /// <c>cosmos_completion_t*</c>, fans the outcome (OK / ERROR / CANCELLED)
    /// onto the matching <see cref="TaskCompletionSource{T}"/>, then frees
    /// the completion handle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Differences vs V1 (NativeAsyncPoc): the spec opaqued the completion
    /// record (V1 returned <c>user_data</c> / <c>status</c> / <c>response</c>
    /// as three out-params from the wait call). The V2 wait returns a single
    /// opaque pointer; the host calls the <c>cosmos_completion_*</c>
    /// accessor family to read each field. The host is responsible for
    /// freeing the completion regardless of outcome — and for freeing the
    /// taken response / error after taking ownership.
    /// </para>
    /// <para>
    /// Spec §3.1.3 "wait returned NULL" disambiguation rule is implemented
    /// here: NULL + state==Running == timeout (continue); NULL + state in
    /// {Shutdown, Drained} == terminal (drain pending, exit).
    /// </para>
    /// </remarks>
    internal sealed class CompletionQueueLoop : IDisposable
    {
        private readonly IntPtr cqHandle;
        private readonly Thread pumpThread;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<CosmosNativeResponse>> pending;
        private long nextUserData;
        private int disposed;

        public CompletionQueueLoop(IntPtr runtime, CosmosCqOptions? options = null)
        {
            CosmosCqOptions opts = options ?? CosmosCqOptions.Default;
            this.cqHandle = cosmos_cq_create(runtime, in opts);
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

        /// <summary>
        /// Reserve a new user_data token and bind it to the TCS the pump
        /// should resolve on completion. Returns an <see cref="IntPtr"/>
        /// already shaped for passing to <see cref="cosmos_driver_submit"/>.
        /// </summary>
        public IntPtr Register(TaskCompletionSource<CosmosNativeResponse> tcs, out ulong token)
        {
            token = (ulong)Interlocked.Increment(ref this.nextUserData);
            this.pending[token] = tcs;
            return new IntPtr((long)token);
        }

        /// <summary>Cancel a registration if the synchronous submit failed.</summary>
        public bool Unregister(ulong token) => this.pending.TryRemove(token, out _);

        private void Pump()
        {
            while (Volatile.Read(ref this.disposed) == 0)
            {
                IntPtr completion = cosmos_cq_wait(this.cqHandle, timeoutMs: 200);
                if (completion == IntPtr.Zero)
                {
                    // Spec §3.1.3 — wait returned NULL. Distinguish timeout
                    // (Running) from terminal (Shutdown/Drained).
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

            // Disposed flag was raised; signal Rust side and drain.
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
                        CosmosNativeResponse materialized = MaterializeResponse(response);
                        tcs.TrySetResult(materialized);
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
                    IntPtr error = cosmos_completion_take_error(completion);
                    Exception ex;
                    try
                    {
                        ex = error == IntPtr.Zero
                            ? new InvalidOperationException("completion outcome=ERROR but cosmos_completion_take_error returned NULL")
                            : new CosmosNativeException(error);
                    }
                    finally
                    {
                        if (error != IntPtr.Zero)
                        {
                            cosmos_error_free(error);
                        }
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
                return new CosmosNativeResponse(
                    http: 0, ru: 0.0,
                    activityId: null, sessionToken: null,
                    etag: null, continuation: null,
                    body: Array.Empty<byte>());
            }

            ushort http = cosmos_response_status_code(response);
            double ru = cosmos_response_request_charge(response);

            string? activityId = null;
            if (cosmos_response_activity_id(response, out IntPtr aPtr) == CosmosErrorCode.Success)
            {
                activityId = PtrToUtf8(aPtr);
            }

            string? sessionToken = null;
            if (cosmos_response_session_token(response, out IntPtr sPtr) == CosmosErrorCode.Success)
            {
                sessionToken = PtrToUtf8(sPtr);
            }

            string? etag = null;
            if (cosmos_response_etag(response, out IntPtr ePtr) == CosmosErrorCode.Success)
            {
                etag = PtrToUtf8(ePtr);
            }

            string? continuation = null;
            if (cosmos_response_continuation_token(response, out IntPtr cPtr) == CosmosErrorCode.Success)
            {
                continuation = PtrToUtf8(cPtr);
            }

            byte[] body;
            CosmosBytesView bodyView = cosmos_response_body(response);
            int len = (int)bodyView.Len.ToUInt32();
            if (bodyView.Data == IntPtr.Zero || len <= 0)
            {
                body = Array.Empty<byte>();
            }
            else
            {
                body = new byte[len];
                Marshal.Copy(bodyView.Data, body, 0, len);
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
            // The pump observes `disposed`, calls cosmos_cq_shutdown, drains.
            // We give it up to 5s to exit cleanly before tearing the handle.
            this.pumpThread.Join(TimeSpan.FromSeconds(5));
            cosmos_cq_free(this.cqHandle);
        }
    }
}
