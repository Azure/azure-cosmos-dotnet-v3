// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The "user_data" payload that round-trips through the Rust FFI.
    /// One instance per submitted operation; GC-rooted by a
    /// <see cref="GCHandle"/> whose <see cref="IntPtr"/> is what we
    /// pass to <c>cosmos_driver_execute_*_submit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Design follows the consensus from the Aaron Robinson + Kevin Jones +
    /// Ashley Schroder Teams thread (May 2026), which favoured the boxed
    /// <c>{ Id, Tcs }</c> shape over a <c>ConcurrentDictionary&lt;ulong, TCS&gt;</c>:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Aaron:</b> <i>"I would avoid the ConcurrentDictionary&lt;&gt;
    ///     unless there is a real need for a disjoint mapping. The GCHandle
    ///     approach is going to defer the mapping and threading issue to
    ///     the GC, which is generally just as good as Rust... That should
    ///     be boxed and shoved into a GCHandle and passed around. I'd
    ///     prefer the box because you want an ID to help fish out problems
    ///     and where things might go wrong (that is, logging and trace)."</i>
    ///   </item>
    ///   <item>
    ///     <b>Kevin:</b> single dedicated pump thread reads from the CQ,
    ///     looks up the TCS via the round-tripped handle, sets the result
    ///     with an asynchronous continuation so the pump thread is not
    ///     tied up by user code.
    ///   </item>
    ///   <item>
    ///     <b>Ashley/Aaron post-decision:</b> the GCHandle approach is the
    ///     right primitive for chatty completion volumes, since it avoids
    ///     the central concurrent dictionary that would otherwise become
    ///     a contention hotspot.
    ///   </item>
    /// </list>
    /// <para>
    /// The blog post that crystallised the approach — Nazarii Piontko,
    /// <i>"Rust↔C# async interop"</i>
    /// (https://www.npiontko.pro/2026/04/26/rust-csharp-async-interop) —
    /// uses an abstract <c>AsyncOperation</c> base with typed
    /// <c>AsyncOperation&lt;T&gt;</c> leaves. Our POC has a single
    /// completion shape (<see cref="CosmosNativeResponse"/>) so we collapse
    /// the hierarchy to one sealed class. If the production binding ever
    /// needs different completion shapes per kind, we can split this into
    /// a base + leaves later without disturbing the routing primitive.
    /// </para>
    /// <para>
    /// <b>Lifetime contract.</b> A <see cref="GCHandle"/> of type
    /// <see cref="GCHandleType.Normal"/> roots this object so the GC will
    /// not collect it while Rust is holding the cookie. The handle is
    /// freed in exactly one place: the CQ pump's dispatch path, right
    /// after it has read <see cref="Tcs"/> and called <c>TrySet*</c>.
    /// The only other freeing path is the pre-flight rollback in
    /// <c>NativeCosmosClient.RunSingletonAsync</c> when
    /// <c>cosmos_driver_execute_*_submit</c> returns NULL — there will
    /// never be a completion in that case, so the submitter owns the free.
    /// </para>
    /// <para>
    /// <b>Why <see cref="GCHandleType.Normal"/> and not <c>Pinned</c>.</b>
    /// Rust never reads the bytes of this object; it only carries the
    /// opaque <see cref="IntPtr"/> from <see cref="GCHandle.ToIntPtr"/>
    /// back to us. A normal handle keeps the object alive and gives a
    /// stable identity (the GCHandle table slot does not move) without
    /// pinning the underlying object in the heap.
    /// </para>
    /// </remarks>
    internal sealed class NativeAsyncOperation
    {
        private static long nextOpId;

        public NativeAsyncOperation()
        {
            this.Id = (ulong)Interlocked.Increment(ref nextOpId);
            this.Tcs = new TaskCompletionSource<CosmosNativeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// Monotonic process-wide id, useful for logging and trace
        /// correlation. Aaron's specific reason for boxing: "you want an
        /// ID to help fish out problems and where things might go wrong."
        /// </summary>
        public ulong Id { get; }

        /// <summary>
        /// The awaiter handed back to the caller. Built with
        /// <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
        /// so <c>TrySetResult</c> never inlines user continuations onto
        /// the pump thread.
        /// </summary>
        public TaskCompletionSource<CosmosNativeResponse> Tcs { get; }

        /// <summary>
        /// Allocate a GC-rooted handle to this operation and return the
        /// stable cookie that <c>user_data</c> will carry through the FFI.
        /// </summary>
        public IntPtr AllocateUserData()
        {
            GCHandle gch = GCHandle.Alloc(this, GCHandleType.Normal);
            return GCHandle.ToIntPtr(gch);
        }

        /// <summary>
        /// Recover the operation from the cookie that came back on a
        /// completion. The caller is responsible for freeing the handle
        /// (<see cref="GCHandle.Free"/>) once it is done with the result —
        /// see <see cref="CompletionQueueLoop"/>'s dispatch path.
        /// </summary>
        public static NativeAsyncOperation FromUserData(IntPtr userData, out GCHandle gch)
        {
            gch = GCHandle.FromIntPtr(userData);
            return (NativeAsyncOperation)gch.Target!;
        }

        public override string ToString() => $"NativeAsyncOperation#{this.Id}";
    }
}
