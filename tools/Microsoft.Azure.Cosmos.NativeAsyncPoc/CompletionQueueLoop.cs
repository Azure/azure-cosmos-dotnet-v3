// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeAsyncPoc
{
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeAsyncPoc.NativeMethods;

    /// <summary>
    /// The .NET-side receive loop for the Rust completion queue. This is the
    /// async-FFI hypothesis on the .NET side: one dedicated host thread per
    /// CQ calls <see cref="NativeMethods.cosmos_cq_wait"/> in a tight loop,
    /// pops completions, and resolves the matching
    /// <see cref="TaskCompletionSource{TResult}"/>.
    /// </summary>
    /// <remarks>
    /// This mirrors the structure of
    /// <c>Microsoft.Azure.Cosmos.Direct.Rntbd2.Connection.ReceiveLoopAsync</c>
    /// in the V3 SDK's Direct transport layer (one background loop per TCP
    /// connection demultiplexes frames to per-request TCS instances). The
    /// only structural difference is that the demultiplex key is an opaque
    /// <see cref="ulong"/> instead of a per-frame correlation id.
    ///
    /// <para>
    /// The opaque-key handoff itself is the same pattern as the .NET runtime's
    /// <c>SocketAsyncEventArgs</c> IOCP completion pump and as
    /// <c>gRPC C-Core</c>'s <c>CompletionQueue</c> binding (both ship a host
    /// thread that drains a kernel/native queue and dispatches to managed
    /// continuations).
    /// </para>
    /// </remarks>
    public sealed class CompletionQueueLoop : IDisposable
    {
        private readonly IntPtr cqHandle;
        private readonly Thread pumpThread;
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<RawResponse>> pending;
        private long nextUserData;
        private int disposed;

        public CompletionQueueLoop(uint capacity = 1024)
        {
            this.cqHandle = cosmos_cq_new(capacity);
            if (this.cqHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("cosmos_cq_new returned NULL");
            }

            this.pending = new ConcurrentDictionary<ulong, TaskCompletionSource<RawResponse>>();
            this.pumpThread = new Thread(this.Pump)
            {
                IsBackground = true,
                Name = "cosmos-native-cq-pump",
            };
            this.pumpThread.Start();
        }

        public IntPtr Handle => this.cqHandle;

        /// <summary>
        /// Registers a TaskCompletionSource and returns the opaque user_data
        /// token to pass to <c>cosmos_*</c> factory functions. The token is
        /// a 64-bit integer; the Rust side stores it as <c>usize</c> and
        /// hands it back verbatim (invariant I1 from <c>INVARIANTS.md</c>).
        /// </summary>
        public ulong Register(TaskCompletionSource<RawResponse> tcs)
        {
            ulong token = (ulong)Interlocked.Increment(ref this.nextUserData);
            this.pending[token] = tcs;
            return token;
        }

        public bool IsPumpThread => Thread.CurrentThread.ManagedThreadId == this.pumpThread.ManagedThreadId;

        private void Pump()
        {
            while (Volatile.Read(ref this.disposed) == 0)
            {
                CosmosStatus waitRc = cosmos_cq_wait(
                    this.cqHandle,
                    timeoutMs: 200,
                    out UIntPtr userDataNative,
                    out CosmosStatus opStatus,
                    out IntPtr responseHandle);

                if (waitRc == CosmosStatus.Cancelled)
                {
                    // 200 ms timeout — no completion yet; loop and check
                    // disposed flag.
                    continue;
                }

                if (waitRc == CosmosStatus.QueueShutdown)
                {
                    // Drained. Fail every still-pending TCS so the host
                    // doesn't deadlock waiting on operations that will
                    // never produce a completion.
                    foreach (var kvp in this.pending)
                    {
                        kvp.Value.TrySetException(
                            new InvalidOperationException("Completion queue was shut down"));
                    }
                    this.pending.Clear();
                    return;
                }

                if (waitRc != CosmosStatus.Ok)
                {
                    Console.Error.WriteLine($"cosmos_cq_wait returned {waitRc}");
                    continue;
                }

                ulong userData = userDataNative.ToUInt64();
                if (!this.pending.TryRemove(userData, out var tcs))
                {
                    if (responseHandle != IntPtr.Zero)
                    {
                        cosmos_response_free(responseHandle);
                    }
                    Console.Error.WriteLine(
                        $"completion for unknown user_data 0x{userData:X16} dropped");
                    continue;
                }

                try
                {
                    RawResponse raw = ReadRawResponseAndFree(responseHandle);
                    if (opStatus == CosmosStatus.Ok)
                    {
                        tcs.TrySetResult(raw);
                    }
                    else if (opStatus == CosmosStatus.Cancelled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else
                    {
                        tcs.TrySetException(new CosmosNativeException(opStatus, raw));
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }
        }

        private static RawResponse ReadRawResponseAndFree(IntPtr responseHandle)
        {
            if (responseHandle == IntPtr.Zero)
            {
                return new RawResponse(0, Array.Empty<byte>());
            }
            try
            {
                ushort http = cosmos_response_status(responseHandle);
                CosmosStatus rc = cosmos_response_body(
                    responseHandle, out IntPtr bodyPtr, out UIntPtr bodyLenNative);
                if (rc != CosmosStatus.Ok)
                {
                    return new RawResponse(http, Array.Empty<byte>());
                }
                int len = (int)bodyLenNative.ToUInt32();
                byte[] copy = new byte[len];
                if (len > 0)
                {
                    Marshal.Copy(bodyPtr, copy, 0, len);
                }
                return new RawResponse(http, copy);
            }
            finally
            {
                cosmos_response_free(responseHandle);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }
            cosmos_cq_shutdown(this.cqHandle);
            this.pumpThread.Join(TimeSpan.FromSeconds(5));
            cosmos_cq_free(this.cqHandle);
        }
    }

    /// <summary>Materialized response handed off to a TCS.</summary>
    public readonly struct RawResponse
    {
        public RawResponse(ushort httpStatus, byte[] body)
        {
            this.HttpStatus = httpStatus;
            this.Body = body;
        }
        public ushort HttpStatus { get; }
        public byte[] Body { get; }
    }

    internal sealed class CosmosNativeException : Exception
    {
        public CosmosNativeException(NativeMethods.CosmosStatus code, RawResponse response)
            : base($"Native Cosmos call failed: code={code}, http={response.HttpStatus}, body={System.Text.Encoding.UTF8.GetString(response.Body)}")
        {
            this.Code = code;
            this.Response = response;
        }
        public NativeMethods.CosmosStatus Code { get; }
        public RawResponse Response { get; }
    }
}
