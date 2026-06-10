// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// V2 host-side client for <c>azurecosmosdriver.dll</c>. Owns the
    /// runtime, account / database / container refs (container resolved
    /// via the driver, not constructed by name), partition key (built
    /// via <c>cosmos_partition_key_builder_*</c>), driver, and CQ;
    /// exposes Task-returning item-CRUD APIs.
    /// </summary>
    internal sealed class NativeCosmosClient : IDisposable
    {
        private readonly IntPtr runtime;
        private readonly IntPtr account;
        private readonly IntPtr database;
        private readonly IntPtr container;
        private readonly IntPtr partitionKey;
        private readonly IntPtr driver;
        private readonly CompletionQueueLoop cq;
        private int disposed;

        public NativeCosmosClient(
            string endpoint,
            string masterKey,
            string databaseId,
            string containerId,
            string partitionKeyValue,
            string? userAgentSuffix = "cosmos-native-driver-poc")
        {
            IntPtr builder = IntPtr.Zero;
            IntPtr stagedRuntime = IntPtr.Zero;
            IntPtr stagedAccount = IntPtr.Zero;
            IntPtr stagedDatabase = IntPtr.Zero;
            IntPtr stagedDriver = IntPtr.Zero;
            IntPtr stagedContainer = IntPtr.Zero;
            IntPtr pkBuilder = IntPtr.Zero;
            IntPtr stagedPk = IntPtr.Zero;
            CompletionQueueLoop? stagedCq = null;

            try
            {
                // 1. Runtime
                builder = cosmos_runtime_builder_new();
                if (builder == IntPtr.Zero)
                {
                    throw new InvalidOperationException("cosmos_runtime_builder_new returned NULL");
                }
                if (!string.IsNullOrEmpty(userAgentSuffix))
                {
                    ThrowOn(cosmos_runtime_builder_with_user_agent_suffix(builder, userAgentSuffix),
                        "cosmos_runtime_builder_with_user_agent_suffix");
                }

                CosmosErrorCode rc = cosmos_runtime_builder_build(builder, out stagedRuntime, out IntPtr buildErr);
                builder = IntPtr.Zero;  // consumed by build per header §1721
                ThrowOnRich(rc, buildErr, "cosmos_runtime_builder_build");
                if (stagedRuntime == IntPtr.Zero)
                {
                    throw new InvalidOperationException("cosmos_runtime_builder_build returned NULL runtime");
                }

                // 2. Account
                rc = cosmos_account_ref_with_master_key(endpoint, masterKey, out stagedAccount, out IntPtr accErr);
                ThrowOnRich(rc, accErr, "cosmos_account_ref_with_master_key");

                // 3. Database (value-type only — never touches the network)
                rc = cosmos_database_ref_create(stagedAccount, databaseId, out stagedDatabase);
                ThrowOn(rc, "cosmos_database_ref_create");

                // 4. Driver (synchronous get-or-create)
                rc = cosmos_driver_get_or_create_blocking(
                    stagedRuntime, stagedAccount, IntPtr.Zero, out stagedDriver, out IntPtr drvErr);
                if (rc != CosmosErrorCode.Success && rc != CosmosErrorCode.OptionsIgnoredOnCacheHit)
                {
                    ThrowOnRich(rc, drvErr, "cosmos_driver_get_or_create_blocking");
                }
                else if (drvErr != IntPtr.Zero)
                {
                    cosmos_error_free(drvErr);
                }

                // 5. Container — RESOLVED via the driver (may round-trip
                //    to gateway on cache miss to learn the container's
                //    partition-key definition).
                rc = cosmos_driver_resolve_container_blocking(
                    stagedRuntime, stagedDriver, databaseId, containerId,
                    out stagedContainer, out IntPtr contErr);
                ThrowOnRich(rc, contErr, "cosmos_driver_resolve_container_blocking");

                // 6. Partition key — string-only convenience does NOT
                //    exist; must use the builder.
                pkBuilder = cosmos_partition_key_builder_new();
                if (pkBuilder == IntPtr.Zero)
                {
                    throw new InvalidOperationException("cosmos_partition_key_builder_new returned NULL");
                }
                ThrowOn(cosmos_partition_key_builder_add_string(pkBuilder, partitionKeyValue),
                    "cosmos_partition_key_builder_add_string");
                rc = cosmos_partition_key_builder_build(pkBuilder, out stagedPk);
                pkBuilder = IntPtr.Zero;  // consumed by build per header §1499
                ThrowOn(rc, "cosmos_partition_key_builder_build");

                // 7. Completion queue
                stagedCq = new CompletionQueueLoop(stagedRuntime);

                // Commit
                this.runtime = stagedRuntime;
                this.account = stagedAccount;
                this.database = stagedDatabase;
                this.driver = stagedDriver;
                this.container = stagedContainer;
                this.partitionKey = stagedPk;
                this.cq = stagedCq;
                stagedRuntime = stagedAccount = stagedDatabase = stagedDriver
                    = stagedContainer = stagedPk = IntPtr.Zero;
                stagedCq = null;
            }
            finally
            {
                if (builder != IntPtr.Zero) cosmos_runtime_builder_free(builder);
                if (pkBuilder != IntPtr.Zero) cosmos_partition_key_builder_free(pkBuilder);
                stagedCq?.Dispose();
                if (stagedPk != IntPtr.Zero) cosmos_partition_key_free(stagedPk);
                if (stagedContainer != IntPtr.Zero) cosmos_container_ref_free(stagedContainer);
                if (stagedDriver != IntPtr.Zero) cosmos_driver_free(stagedDriver);
                if (stagedDatabase != IntPtr.Zero) cosmos_database_ref_free(stagedDatabase);
                if (stagedAccount != IntPtr.Zero) cosmos_account_ref_free(stagedAccount);
                if (stagedRuntime != IntPtr.Zero) cosmos_runtime_free(stagedRuntime);
            }
        }

        public CompletionQueueLoop CompletionQueue => this.cq;

        public Task<CosmosNativeResponse> ReadItemAsync(string itemId, CancellationToken ct = default) =>
            this.RunSingletonAsync(b => b
                .WithKind(CosmosOperationKind.ReadItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId), ct);

        public Task<CosmosNativeResponse> CreateItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunSingletonAsync(b => b
                .WithKind(CosmosOperationKind.CreateItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), ct);

        public Task<CosmosNativeResponse> UpsertItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunSingletonAsync(b => b
                .WithKind(CosmosOperationKind.UpsertItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), ct);

        public Task<CosmosNativeResponse> ReplaceItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunSingletonAsync(b => b
                .WithKind(CosmosOperationKind.ReplaceItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), ct);

        public Task<CosmosNativeResponse> DeleteItemAsync(string itemId, CancellationToken ct = default) =>
            this.RunSingletonAsync(b => b
                .WithKind(CosmosOperationKind.DeleteItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId), ct);

        /// <summary>
        /// Core dispatch path against the post-PR-#4515 spec.
        ///
        /// Builds a <see cref="CosmosOperationRequest"/> via the supplied
        /// <paramref name="configure"/> action, dispatches through
        /// <c>cosmos_driver_execute_singleton_operation_submit</c>, and
        /// wires the resulting opaque handle into the CQ pump (which
        /// completes the TCS once the Rust side posts a completion).
        ///
        /// Routing model: Aaron Robinson + Kevin Jones + Ashley Schroder
        /// consensus (May 2026 Teams thread). The <c>user_data</c> cookie
        /// we hand to Rust is a <see cref="GCHandle"/> rooting a boxed
        /// <see cref="NativeAsyncOperation"/> (with a diagnostic
        /// <see cref="NativeAsyncOperation.Id"/> for trace correlation
        /// and a <see cref="TaskCompletionSource{T}"/> for the result).
        /// No <c>ConcurrentDictionary&lt;ulong, TCS&gt;</c>, no monotonic
        /// counter — the GCHandle table is the routing primitive.
        ///
        /// Lifetime contract:
        ///   * Every pointer in the request struct (UTF-8 strings, body
        ///     bytes, options sub-struct) is owned by the
        ///     <see cref="CosmosOperationRequestBuilder"/> and lives in
        ///     pinned / CoTaskMem-allocated memory.
        ///   * The wrapper deep-copies before the submit call returns
        ///     (header line 829 + 848), so the builder is safe to dispose
        ///     immediately after the synchronous submit returns.
        ///   * The opaque <c>cosmos_operation_handle_t*</c> is freed on the
        ///     completion side (ContinueWith below), not here.
        ///   * The <see cref="GCHandle"/> is freed by the CQ pump's
        ///     <c>DispatchCompletion</c> after it has settled the TCS —
        ///     except on the pre-flight rollback path (submit returned
        ///     NULL), where we free it inline here because no completion
        ///     will ever arrive.
        /// </summary>
        private Task<CosmosNativeResponse> RunSingletonAsync(
            Action<CosmosOperationRequestBuilder> configure,
            CancellationToken ct)
        {
            var op = new NativeAsyncOperation();

            if (ct.IsCancellationRequested)
            {
                op.Tcs.TrySetCanceled(ct);
                return op.Tcs.Task;
            }

            // 1. Allocate the GCHandle that will keep `op` alive across
            //    the FFI boundary. The IntPtr it produces IS the user_data
            //    cookie Rust will round-trip back to the pump.
            IntPtr userData = op.AllocateUserData();

            IntPtr opHandle;
            CosmosErrorCode preError;
            using (var builder = new CosmosOperationRequestBuilder())
            {
                try
                {
                    configure(builder);
                }
                catch (Exception ex)
                {
                    // Pre-flight rollback: no completion will arrive, so
                    // we free the GCHandle ourselves before failing the TCS.
                    GCHandle.FromIntPtr(userData).Free();
                    op.Tcs.TrySetException(ex);
                    return op.Tcs.Task;
                }

                // 2. Submit — the wrapper deep-copies all borrowed pointers
                //    before returning, so the builder's pinned/Marshal-
                //    allocated memory is safe to release on `using` exit.
                opHandle = builder.Submit(
                    this.driver, this.cq.Handle, userData,
                    OperationBucket.Singleton, out preError);
            }

            if (opHandle == IntPtr.Zero)
            {
                // Pre-flight rollback on the Rust side: no completion will
                // arrive, so we own the GCHandle free.
                GCHandle.FromIntPtr(userData).Free();
                op.Tcs.TrySetException(new InvalidOperationException(
                    $"cosmos_driver_execute_singleton_operation_submit pre-flight rejected: {preError} (op#{op.Id})"));
                return op.Tcs.Task;
            }

            // 3. Wire CancellationToken → cosmos_operation_handle_cancel.
            CancellationTokenRegistration ctr = default;
            if (ct.CanBeCanceled)
            {
                ctr = ct.Register(static state =>
                {
                    cosmos_operation_handle_cancel((IntPtr)state!);
                }, opHandle);
            }

            // 4. Release the op handle (and unregister ct hook) once
            //    the pump has observed completion and resolved the TCS.
            //    Note: the GCHandle is freed by the pump in DispatchCompletion,
            //    so this continuation only needs to clean up the op handle
            //    and the CT registration.
            op.Tcs.Task.ContinueWith(static (_, state) =>
            {
                var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
                st.Item2.Dispose();
                cosmos_operation_handle_free(st.Item1);
            }, Tuple.Create(opHandle, ctr), TaskContinuationOptions.ExecuteSynchronously);

            return op.Tcs.Task;
        }

        private static void ThrowOn(CosmosErrorCode rc, string call)
        {
            if (rc != CosmosErrorCode.Success)
            {
                throw new InvalidOperationException($"{call} failed: {rc}");
            }
        }

        private static void ThrowOnRich(CosmosErrorCode rc, IntPtr err, string call)
        {
            if (rc == CosmosErrorCode.Success)
            {
                if (err != IntPtr.Zero) cosmos_error_free(err);
                return;
            }
            if (err != IntPtr.Zero)
            {
                CosmosNativeException ex;
                try
                {
                    ex = new CosmosNativeException(err, rc);
                }
                finally
                {
                    cosmos_error_free(err);
                }
                throw new InvalidOperationException($"{call} failed: {rc}", ex);
            }
            throw new InvalidOperationException($"{call} failed: {rc}");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }
            // Free children before parents.
            this.cq.Dispose();
            cosmos_partition_key_free(this.partitionKey);
            cosmos_container_ref_free(this.container);
            cosmos_driver_free(this.driver);
            cosmos_database_ref_free(this.database);
            cosmos_account_ref_free(this.account);
            cosmos_runtime_free(this.runtime);
        }
    }
}
