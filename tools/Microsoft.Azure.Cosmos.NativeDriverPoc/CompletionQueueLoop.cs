// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// Receive loop for the merged-PR-#4515
    /// <c>azure_data_cosmos_driver_native</c> completion queue. One
    /// background thread per CQ calls
    /// <see cref="NativeMethods.cosmos_completion_queue_wait"/>, which fills a
    /// caller-allocated by-value <see cref="CosmosCompletion"/> slot, fans the
    /// outcome onto the matching <see cref="TaskCompletionSource{T}"/>, then
    /// releases the slot's borrowed backing via
    /// <see cref="NativeMethods.cosmos_completion_queue_free_completions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Contract change vs the pre-merge draft.</b> The old opaque
    /// <c>cosmos_completion_t*</c> + accessor family (and the separate
    /// <c>cosmos_response_t</c> / <c>cosmos_error_t</c> objects) were removed.
    /// Every output now lives inline on the completion struct; we copy all
    /// strings / body bytes out before <c>free_completions</c> reclaims them.
    /// </para>
    /// <para>
    /// <b>Routing model (Option C — GCHandle).</b> The <c>user_data</c> cookie
    /// Rust round-trips is a <see cref="GCHandle.ToIntPtr"/> of a
    /// <see cref="NativeAsyncOperation"/>. The pump recovers the operation via
    /// <see cref="GCHandle.FromIntPtr"/>, settles its
    /// <see cref="NativeAsyncOperation.Tcs"/>, then frees the handle exactly
    /// once per completion. No central dictionary; the GCHandle table is the
    /// routing primitive.
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
                ? cosmos_completion_queue_create(runtime, options.Value)
                : cosmos_completion_queue_create_default(runtime, IntPtr.Zero);
            if (this.cqHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("cosmos_completion_queue_create returned NULL");
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
                if (!this.DrainOne(timeoutMs: 200))
                {
                    // NULL/0 drained — check terminal state.
                    CosmosCqState state = cosmos_completion_queue_state(this.cqHandle);
                    if (state == CosmosCqState.Running)
                    {
                        continue;
                    }
                    return;
                }
            }

            // Disposed flag was set — ask Rust to drain in-flight ops and
            // process every completion it surfaces so the awaiters wake up.
            cosmos_completion_queue_shutdown(this.cqHandle);
            this.DrainAfterShutdown();
        }

        /// <summary>
        /// After <c>cosmos_completion_queue_shutdown</c> we keep pumping until
        /// <c>wait</c> drains nothing with the CQ in a terminal state.
        /// </summary>
        private void DrainAfterShutdown()
        {
            while (true)
            {
                if (!this.DrainOne(timeoutMs: 50))
                {
                    CosmosCqState state = cosmos_completion_queue_state(this.cqHandle);
                    if (state == CosmosCqState.Running)
                    {
                        // Shouldn't happen post-shutdown; defensive re-arm.
                        continue;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Waits for a single completion, dispatches it, and releases its
        /// backing. Returns true iff a completion was drained.
        /// </summary>
        private bool DrainOne(uint timeoutMs)
        {
            CosmosCompletion slot = default;
            UIntPtr drained = cosmos_completion_queue_wait(
                this.cqHandle, ref slot, (UIntPtr)1, timeoutMs);
            if (drained == UIntPtr.Zero)
            {
                return false;
            }

            try
            {
                this.DispatchCompletion(ref slot);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[pump] dispatch error: {ex}");
            }
            finally
            {
                // Reclaims the slot's borrowed backing (and any owned
                // driver/container handle we did not detach).
                cosmos_completion_queue_free_completions(ref slot, (UIntPtr)1);
            }
            return true;
        }

        private void DispatchCompletion(ref CosmosCompletion completion)
        {
            IntPtr userData = completion.UserData;
            if (userData == IntPtr.Zero)
            {
                Console.Error.WriteLine("[pump] completion with NULL user_data dropped");
                return;
            }

            // GCHandle is the routing primitive — no dict, no lookup.
            NativeAsyncOperation op;
            GCHandle gch;
            try
            {
                op = NativeAsyncOperation.FromUserData(userData, out gch);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[pump] completion for invalid user_data 0x{userData.ToInt64():X16} dropped: {ex.Message}");
                return;
            }

            try
            {
                SettleCompletion(op, ref completion);
            }
            finally
            {
                // Free the GCHandle exactly once per completion.
                gch.Free();
            }
        }

        private static void SettleCompletion(NativeAsyncOperation op, ref CosmosCompletion completion)
        {
            switch (completion.Outcome)
            {
                case CosmosCompletionOutcome.Ok:
                {
                    op.Tcs.TrySetResult(MaterializeResponse(ref completion));
                    break;
                }

                case CosmosCompletionOutcome.Error:
                {
                    // Every error field is inline on the completion struct now
                    // (no separate cosmos_error_t). Copy them into a managed
                    // exception before the backing is freed.
                    var ex = new CosmosNativeException(
                        completion.Status,
                        completion.HttpStatusCode,
                        completion.SubStatus,
                        completion.IsFromWire != 0,
                        PtrToUtf8(completion.Message),
                        PtrToUtf8(completion.ActivityId),
                        PtrToUtf8(completion.SessionToken),
                        PtrToUtf8(completion.Etag),
                        completion.RetryAfterMs,
                        PtrToUtf8(completion.Backtrace));
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
                        $"unexpected cosmos_completion outcome: {completion.Outcome}"));
                    break;
                }
            }
        }

        /// <summary>
        /// Copy every borrowed field out of the by-value completion into a
        /// managed <see cref="CosmosNativeResponse"/>. MUST run before
        /// <c>free_completions</c> reclaims the backing.
        /// </summary>
        private static CosmosNativeResponse MaterializeResponse(ref CosmosCompletion completion)
        {
            byte[] body = Array.Empty<byte>();
            if (completion.Body != IntPtr.Zero)
            {
                int len = checked((int)completion.BodyLen.ToUInt64());
                if (len > 0)
                {
                    body = new byte[len];
                    Marshal.Copy(completion.Body, body, 0, len);
                }
            }

            return new CosmosNativeResponse(
                completion.HttpStatusCode,
                completion.RequestCharge,
                PtrToUtf8(completion.ActivityId),
                PtrToUtf8(completion.SessionToken),
                PtrToUtf8(completion.Etag),
                PtrToUtf8(completion.Continuation),
                PtrToUtf8(completion.NextContinuation),
                body);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }
            this.pumpThread.Join(TimeSpan.FromSeconds(5));
            cosmos_completion_queue_free(this.cqHandle);
        }
    }
}
