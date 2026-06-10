// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
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
    /// <remarks>
    /// <para>
    /// <b>Routing model (Option C — GCHandle).</b> The <c>user_data</c> cookie
    /// that Rust round-trips is a <see cref="GCHandle.ToIntPtr"/> of a
    /// <see cref="NativeAsyncOperation"/> instance allocated by the
    /// submitter. The pump recovers the operation via
    /// <see cref="GCHandle.FromIntPtr"/>, sets the result on its
    /// <see cref="NativeAsyncOperation.Tcs"/>, then frees the handle —
    /// exactly once per submission. No central dictionary, no shared
    /// counter. The CLR's GCHandle table is the routing primitive.
    /// </para>
    /// <para>
    /// This design follows the consensus in Aaron Robinson + Kevin Jones +
    /// Ashley Schroder's Teams thread (May 2026); see
    /// <see cref="NativeAsyncOperation"/> for the verbatim recommendations
    /// and the blog reference that crystallised the approach.
    /// </para>
    /// <para>
    /// <b>Shutdown contract.</b> On <see cref="Dispose"/>, the pump exits
    /// its wait loop, calls <c>cosmos_cq_shutdown</c>, then drains any
    /// remaining completions through the normal dispatch path before
    /// freeing the CQ handle. Rust's <c>cosmos_cq_shutdown</c> is
    /// documented to post completions (typically with the
    /// <see cref="CosmosCompletionOutcome.Cancelled"/> outcome) for every
    /// in-flight op, so awaiters wake up cleanly with a
    /// <see cref="TaskCanceledException"/> instead of hanging. If a future
    /// Rust change breaks that guarantee we'd see GCHandle table growth
    /// across client lifetimes and add a defensive outstanding-handles
    /// set here; today we trust the contract.
    /// </para>
    /// </remarks>
    internal sealed class CompletionQueueLoop : IDisposable
    {
        private readonly IntPtr cqHandle;
        private readonly Thread pumpThread;
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

            this.pumpThread = new Thread(this.Pump)
            {
                IsBackground = true,
                Name = "cosmos-native-driver-cq-pump",
            };
            this.pumpThread.Start();
        }

        public IntPtr Handle => this.cqHandle;

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

            // Disposed flag was set — ask Rust to drain in-flight ops and
            // process every completion it surfaces so the awaiters wake up.
            cosmos_cq_shutdown(this.cqHandle);
            this.DrainAfterShutdown();
        }

        /// <summary>
        /// After <c>cosmos_cq_shutdown</c> we keep pumping until
        /// <c>cosmos_cq_wait</c> returns NULL with the CQ in a terminal
        /// state. Every completion observed in this phase still routes
        /// through <see cref="DispatchCompletion"/>, which frees its
        /// <see cref="GCHandle"/> normally — so the in-flight set drains
        /// without us tracking it.
        /// </summary>
        private void DrainAfterShutdown()
        {
            while (true)
            {
                IntPtr completion = cosmos_cq_wait(this.cqHandle, timeoutMs: 50);
                if (completion == IntPtr.Zero)
                {
                    CosmosCqState state = cosmos_cq_state(this.cqHandle);
                    if (state == CosmosCqState.Running)
                    {
                        // Shouldn't happen post-shutdown; defensive re-arm.
                        continue;
                    }
                    return;
                }

                try
                {
                    this.DispatchCompletion(completion);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[pump-drain] dispatch error: {ex}");
                }
                finally
                {
                    cosmos_completion_free(completion);
                }
            }
        }

        private void DispatchCompletion(IntPtr completion)
        {
            IntPtr userData = cosmos_completion_user_data(completion);
            if (userData == IntPtr.Zero)
            {
                Console.Error.WriteLine("[pump] completion with NULL user_data dropped");
                return;
            }

            // GCHandle is the routing primitive — no dict, no lookup.
            // The handle's Target field IS the NativeAsyncOperation we
            // stashed at submit time.
            NativeAsyncOperation op;
            GCHandle gch;
            try
            {
                op = NativeAsyncOperation.FromUserData(userData, out gch);
            }
            catch (Exception ex)
            {
                // FromIntPtr throws InvalidOperationException on an
                // invalidated handle — typically a double-delivery or a
                // handle that was already freed via the pre-flight
                // rollback path. Drop and log; never let it crash the pump.
                Console.Error.WriteLine(
                    $"[pump] completion for invalid user_data 0x{userData.ToInt64():X16} dropped: {ex.Message}");
                return;
            }

            try
            {
                SettleCompletion(op, completion);
            }
            finally
            {
                // Free the GCHandle exactly once per completion — this is
                // the spot. After this line the IntPtr is invalid; any
                // duplicate delivery from Rust will be caught by the
                // try/catch above.
                gch.Free();
            }
        }

        private static void SettleCompletion(NativeAsyncOperation op, IntPtr completion)
        {
            CosmosCompletionOutcome outcome = cosmos_completion_outcome(completion);

            switch (outcome)
            {
                case CosmosCompletionOutcome.Ok:
                {
                    IntPtr response = cosmos_completion_take_response(completion);
                    try
                    {
                        op.Tcs.TrySetResult(MaterializeResponse(response));
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
                    op.Tcs.TrySetException(ex);
                    break;
                }

                case CosmosCompletionOutcome.Cancelled:
                {
                    op.Tcs.TrySetCanceled();
                    break;
                }

                default:
                {
                    op.Tcs.TrySetException(new InvalidOperationException(
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
