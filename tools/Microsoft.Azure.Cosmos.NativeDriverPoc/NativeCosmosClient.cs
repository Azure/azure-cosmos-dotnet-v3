// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// V2 host-side client for the production-spec
    /// <c>azure_data_cosmos_driver_native</c> surface. Owns the runtime,
    /// account/db/container refs, partition key, driver, and completion
    /// queue; exposes Task-returning item APIs that .NET callers can
    /// await idiomatically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Object-graph mirror of spec §4: builder → runtime → account ref →
    /// (db ref → container ref) and (driver via blocking get-or-create) →
    /// op factory → submit → completion. Partition key is built once for
    /// the seeded fixture and reused; a real SDK would build one per
    /// operation.
    /// </para>
    /// <para>
    /// Cancellation: <see cref="cosmos_operation_handle_cancel"/> requests
    /// cooperative cancellation. The Rust side honors it (notify + biased
    /// select), eventually posting a completion with outcome=CANCELLED
    /// (which the pump translates to TCS.TrySetCanceled) or, if cancel
    /// lost the race, outcome=OK / ERROR with
    /// <c>cosmos_completion_was_cancel_requested</c>=true.
    /// </para>
    /// </remarks>
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
            uint workerThreads = 0)
        {
            IntPtr builder = IntPtr.Zero;
            IntPtr stagedRuntime = IntPtr.Zero;
            IntPtr stagedAccount = IntPtr.Zero;
            IntPtr stagedDatabase = IntPtr.Zero;
            IntPtr stagedContainer = IntPtr.Zero;
            IntPtr stagedPk = IntPtr.Zero;
            IntPtr stagedDriver = IntPtr.Zero;
            CompletionQueueLoop? stagedCq = null;

            try
            {
                // 1. Runtime
                builder = cosmos_runtime_builder_new();
                if (builder == IntPtr.Zero)
                {
                    throw new InvalidOperationException("cosmos_runtime_builder_new returned NULL");
                }
                if (workerThreads > 0)
                {
                    ThrowOn(cosmos_runtime_builder_with_worker_threads(builder, workerThreads),
                        "cosmos_runtime_builder_with_worker_threads");
                }
                ThrowOn(cosmos_runtime_builder_with_thread_name_prefix(builder, "cosmos-poc"),
                    "cosmos_runtime_builder_with_thread_name_prefix");
                // Emulator self-signed cert — accepted only against localhost.
                ThrowOn(cosmos_runtime_builder_with_allow_emulator_invalid_certs(builder, true),
                    "cosmos_runtime_builder_with_allow_emulator_invalid_certs");

                CosmosErrorCode rc = cosmos_runtime_builder_build(builder, out stagedRuntime, out IntPtr buildErr);
                builder = IntPtr.Zero; // consumed by build per spec §4.1
                ThrowOnRich(rc, buildErr, "cosmos_runtime_builder_build");
                if (stagedRuntime == IntPtr.Zero)
                {
                    throw new InvalidOperationException("cosmos_runtime_builder_build returned NULL runtime");
                }

                // 2. Account / DB / Container refs
                rc = cosmos_account_ref_with_master_key(endpoint, masterKey, out stagedAccount, out IntPtr accErr);
                ThrowOnRich(rc, accErr, "cosmos_account_ref_with_master_key");

                rc = cosmos_database_ref_create(stagedAccount, databaseId, out stagedDatabase);
                ThrowOn(rc, "cosmos_database_ref_create");

                rc = cosmos_container_ref_create(stagedDatabase, containerId, out stagedContainer);
                ThrowOn(rc, "cosmos_container_ref_create");

                // 3. Partition key — built once and reused for this POC.
                rc = cosmos_partition_key_from_string(partitionKeyValue, out stagedPk);
                ThrowOn(rc, "cosmos_partition_key_from_string");

                // 4. Driver (cache-keyed on endpoint; options dropped on hit per spec §4.4)
                rc = cosmos_driver_get_or_create_blocking(
                    stagedRuntime, stagedAccount, IntPtr.Zero, out stagedDriver, out IntPtr drvErr);
                if (rc != CosmosErrorCode.Success && rc != CosmosErrorCode.OptionsIgnoredOnCacheHit)
                {
                    ThrowOnRich(rc, drvErr, "cosmos_driver_get_or_create_blocking");
                }
                else if (drvErr != IntPtr.Zero)
                {
                    // OptionsIgnoredOnCacheHit may surface an advisory error — log & free.
                    Console.Error.WriteLine("[driver] advisory: options ignored on cache hit");
                    cosmos_error_free(drvErr);
                }

                // 5. Completion queue
                stagedCq = new CompletionQueueLoop(stagedRuntime);

                // Commit
                this.runtime = stagedRuntime;
                this.account = stagedAccount;
                this.database = stagedDatabase;
                this.container = stagedContainer;
                this.partitionKey = stagedPk;
                this.driver = stagedDriver;
                this.cq = stagedCq;
                stagedRuntime = stagedAccount = stagedDatabase = stagedContainer = stagedPk = stagedDriver = IntPtr.Zero;
                stagedCq = null;
            }
            finally
            {
                if (builder != IntPtr.Zero) cosmos_runtime_builder_free(builder);
                stagedCq?.Dispose();
                if (stagedDriver != IntPtr.Zero) cosmos_driver_free(stagedDriver);
                if (stagedPk != IntPtr.Zero) cosmos_partition_key_free(stagedPk);
                if (stagedContainer != IntPtr.Zero) cosmos_container_ref_free(stagedContainer);
                if (stagedDatabase != IntPtr.Zero) cosmos_database_ref_free(stagedDatabase);
                if (stagedAccount != IntPtr.Zero) cosmos_account_ref_free(stagedAccount);
                if (stagedRuntime != IntPtr.Zero) cosmos_runtime_free(stagedRuntime);
            }
        }

        public CompletionQueueLoop CompletionQueue => this.cq;

        /// <summary>
        /// Read a single item by id (uses the partition key supplied at
        /// construction time). The returned Task completes when the
        /// pump observes the matching completion.
        /// </summary>
        public Task<CosmosNativeResponse> ReadItemAsync(
            string itemId,
            CancellationToken cancellationToken = default)
        {
            return this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_read_item(this.container, itemId, this.partitionKey, out op),
                bodyJson: null,
                cancellationToken);
        }

        /// <summary>
        /// Create an item with the supplied JSON body. Body MUST contain
        /// the partition-key field whose value matches the partition key
        /// supplied at construction time (this POC reuses one PK).
        /// </summary>
        public Task<CosmosNativeResponse> CreateItemAsync(
            string itemId,
            string bodyJson,
            CancellationToken cancellationToken = default)
        {
            return this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_create_item(this.container, itemId, this.partitionKey, out op),
                bodyJson,
                cancellationToken);
        }

        public Task<CosmosNativeResponse> UpsertItemAsync(
            string itemId,
            string bodyJson,
            CancellationToken cancellationToken = default)
        {
            return this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_upsert_item(this.container, itemId, this.partitionKey, out op),
                bodyJson,
                cancellationToken);
        }

        public Task<CosmosNativeResponse> ReplaceItemAsync(
            string itemId,
            string bodyJson,
            CancellationToken cancellationToken = default)
        {
            return this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_replace_item(this.container, itemId, this.partitionKey, out op),
                bodyJson,
                cancellationToken);
        }

        public Task<CosmosNativeResponse> DeleteItemAsync(
            string itemId,
            CancellationToken cancellationToken = default)
        {
            return this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_delete_item(this.container, itemId, this.partitionKey, out op),
                bodyJson: null,
                cancellationToken);
        }

        private delegate CosmosErrorCode OperationFactory(out IntPtr op);

        private Task<CosmosNativeResponse> RunOperationAsync(
            OperationFactory factory,
            string? bodyJson,
            CancellationToken cancellationToken)
        {
            // RunContinuationsAsynchronously: keep the CQ pump thread doing
            // pump work, hand user continuations to the thread pool.
            var tcs = new TaskCompletionSource<CosmosNativeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return tcs.Task;
            }

            // 1. Build the operation
            CosmosErrorCode rc = factory(out IntPtr op);
            if (rc != CosmosErrorCode.Success || op == IntPtr.Zero)
            {
                tcs.TrySetException(new InvalidOperationException(
                    $"operation factory failed: {rc}"));
                return tcs.Task;
            }

            // 2. Attach body if needed (data is COPIED into wrapper storage per spec §4.6.2)
            GCHandle bodyPin = default;
            if (bodyJson != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(bodyJson);
                bodyPin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    var view = new CosmosBytesView
                    {
                        Data = bodyPin.AddrOfPinnedObject(),
                        Len = (UIntPtr)bytes.Length,
                    };
                    rc = cosmos_operation_with_body(op, view);
                }
                finally
                {
                    bodyPin.Free();
                }
                if (rc != CosmosErrorCode.Success)
                {
                    cosmos_operation_free(op);
                    tcs.TrySetException(new InvalidOperationException(
                        $"cosmos_operation_with_body failed: {rc}"));
                    return tcs.Task;
                }
            }

            // 3. Register TCS + user_data token
            IntPtr userData = this.cq.Register(tcs, out ulong token);

            // 4. Submit — consumes op on success, returns op handle for cancel/poll
            IntPtr opHandle = cosmos_driver_submit(
                this.driver, op, IntPtr.Zero, this.cq.Handle, userData, out CosmosErrorCode preError);

            if (opHandle == IntPtr.Zero)
            {
                // Pre-flight rejection — nothing will arrive on the queue.
                this.cq.Unregister(token);
                cosmos_operation_free(op);
                tcs.TrySetException(new InvalidOperationException(
                    $"cosmos_driver_submit pre-flight rejected: {preError}"));
                return tcs.Task;
            }

            // 5. Wire cancellation
            CancellationTokenRegistration ctr = default;
            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(static state =>
                {
                    cosmos_operation_handle_cancel((IntPtr)state!);
                }, opHandle);
            }

            // 6. Free op handle (and unregister cancel hook) once completion is observed
            tcs.Task.ContinueWith(static (_, state) =>
            {
                var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
                st.Item2.Dispose();
                cosmos_operation_handle_free(st.Item1);
            }, Tuple.Create(opHandle, ctr), TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
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
                    ex = new CosmosNativeException(err);
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
            // Spec §4.3, §4.4, §4.5: free children before parents.
            this.cq.Dispose();
            cosmos_driver_free(this.driver);
            cosmos_partition_key_free(this.partitionKey);
            cosmos_container_ref_free(this.container);
            cosmos_database_ref_free(this.database);
            cosmos_account_ref_free(this.account);
            cosmos_runtime_free(this.runtime);
        }
    }
}
