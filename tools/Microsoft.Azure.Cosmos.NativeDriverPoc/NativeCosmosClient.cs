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
            this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_read_item(this.container, itemId, this.partitionKey, out op),
                bodyJson: null, ct);

        public Task<CosmosNativeResponse> CreateItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_create_item(this.container, itemId, this.partitionKey, out op),
                bodyJson, ct);

        public Task<CosmosNativeResponse> UpsertItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_upsert_item(this.container, itemId, this.partitionKey, out op),
                bodyJson, ct);

        public Task<CosmosNativeResponse> ReplaceItemAsync(string itemId, string bodyJson, CancellationToken ct = default) =>
            this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_replace_item(this.container, itemId, this.partitionKey, out op),
                bodyJson, ct);

        public Task<CosmosNativeResponse> DeleteItemAsync(string itemId, CancellationToken ct = default) =>
            this.RunOperationAsync(
                (out IntPtr op) => cosmos_operation_delete_item(this.container, itemId, this.partitionKey, out op),
                bodyJson: null, ct);

        private delegate CosmosErrorCode OperationFactory(out IntPtr op);

        private Task<CosmosNativeResponse> RunOperationAsync(
            OperationFactory factory, string? bodyJson, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<CosmosNativeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return tcs.Task;
            }

            // 1. Build the operation
            CosmosErrorCode rc = factory(out IntPtr op);
            if (rc != CosmosErrorCode.Success || op == IntPtr.Zero)
            {
                tcs.TrySetException(new InvalidOperationException($"operation factory failed: {rc}"));
                return tcs.Task;
            }

            // 2. Attach body — copied into wrapper storage per header §1251
            if (bodyJson != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(bodyJson);
                rc = cosmos_operation_with_body(op, bytes, (UIntPtr)bytes.Length);
                if (rc != CosmosErrorCode.Success)
                {
                    cosmos_operation_free(op);
                    tcs.TrySetException(new InvalidOperationException($"cosmos_operation_with_body failed: {rc}"));
                    return tcs.Task;
                }
            }

            // 3. Register TCS
            IntPtr userData = this.cq.Register(tcs, out ulong token);

            // 4. Submit — consumes op on success per header §1764
            IntPtr opHandle = cosmos_driver_submit(
                this.driver, op, IntPtr.Zero, this.cq.Handle, userData, out CosmosErrorCode preError);

            if (opHandle == IntPtr.Zero)
            {
                this.cq.Unregister(token);
                cosmos_operation_free(op);
                tcs.TrySetException(new InvalidOperationException(
                    $"cosmos_driver_submit pre-flight rejected: {preError}"));
                return tcs.Task;
            }

            // 5. Wire CancellationToken → cosmos_operation_handle_cancel
            CancellationTokenRegistration ctr = default;
            if (ct.CanBeCanceled)
            {
                ctr = ct.Register(static state =>
                {
                    cosmos_operation_handle_cancel((IntPtr)state!);
                }, opHandle);
            }

            // 6. Release op handle (and unregister ct hook) once completion observed
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
