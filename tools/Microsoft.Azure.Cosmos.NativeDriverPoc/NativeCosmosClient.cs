// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Construct a client pinned to a single-component partition key.
        /// All CRUD + query operations on this client will target that PK.
        /// </summary>
        public NativeCosmosClient(
            string endpoint,
            string masterKey,
            string databaseId,
            string containerId,
            string partitionKeyValue,
            string? userAgentSuffix = "cosmos-native-driver-poc")
            : this(endpoint, masterKey, databaseId, containerId,
                   () => BuildPartitionKey(new[] { (object)partitionKeyValue }),
                   userAgentSuffix)
        {
        }

        /// <summary>
        /// Construct a client pinned to a hierarchical partition key
        /// (multi-component, e.g. <c>["tenant-A","region-1","user-42"]</c>).
        /// Each component is added to the builder via
        /// <c>cosmos_partition_key_builder_add_string</c> in array order;
        /// the resulting PK must match the container's HPK definition
        /// (same number of paths, in the same order).
        /// </summary>
        public NativeCosmosClient(
            string endpoint,
            string masterKey,
            string databaseId,
            string containerId,
            string[] hpkComponents,
            string? userAgentSuffix = "cosmos-native-driver-poc-hpk")
            : this(endpoint, masterKey, databaseId, containerId,
                   () => BuildPartitionKey(Array.ConvertAll(hpkComponents ?? throw new ArgumentNullException(nameof(hpkComponents)), c => (object)c)),
                   userAgentSuffix)
        {
        }

        /// <summary>
        /// Construct a client with NO pinned partition key. Only
        /// cross-partition query methods are valid on the returned
        /// client; calling any CRUD method will throw. Cross-partition
        /// queries omit <c>WithPartitionKey</c> entirely on the request
        /// (header §449 makes <c>partition_key</c> optional for
        /// <c>QueryItems</c>), which signals the driver to fan out.
        /// </summary>
        public static NativeCosmosClient CreateCrossPartition(
            string endpoint,
            string masterKey,
            string databaseId,
            string containerId,
            string? userAgentSuffix = "cosmos-native-driver-poc-xpart")
        {
            return new NativeCosmosClient(
                endpoint, masterKey, databaseId, containerId,
                buildPk: () => IntPtr.Zero,
                userAgentSuffix: userAgentSuffix);
        }

        /// <summary>
        /// Canonical ctor. The PK-construction strategy is supplied as
        /// a delegate so the three public surfaces (single-PK, HPK,
        /// cross-partition) share the rest of the wiring. The delegate
        /// is invoked inside the staged-rollback try block, so a PK
        /// build failure does not leak the runtime/account/etc. The
        /// delegate may return <see cref="IntPtr.Zero"/> to signal "no
        /// pinned PK" (cross-partition queries only).
        /// </summary>
        private NativeCosmosClient(
            string endpoint,
            string masterKey,
            string databaseId,
            string containerId,
            Func<IntPtr> buildPk,
            string? userAgentSuffix)
        {
            IntPtr builder = IntPtr.Zero;
            IntPtr stagedRuntime = IntPtr.Zero;
            IntPtr stagedAccount = IntPtr.Zero;
            IntPtr stagedDatabase = IntPtr.Zero;
            IntPtr stagedDriver = IntPtr.Zero;
            IntPtr stagedContainer = IntPtr.Zero;
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

                // 6. Partition key — delegated. Returning IntPtr.Zero
                //    means "no pinned PK" (cross-partition client).
                stagedPk = buildPk();

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
                stagedCq?.Dispose();
                if (stagedPk != IntPtr.Zero) cosmos_partition_key_free(stagedPk);
                if (stagedContainer != IntPtr.Zero) cosmos_container_ref_free(stagedContainer);
                if (stagedDriver != IntPtr.Zero) cosmos_driver_free(stagedDriver);
                if (stagedDatabase != IntPtr.Zero) cosmos_database_ref_free(stagedDatabase);
                if (stagedAccount != IntPtr.Zero) cosmos_account_ref_free(stagedAccount);
                if (stagedRuntime != IntPtr.Zero) cosmos_runtime_free(stagedRuntime);
            }
        }

        /// <summary>
        /// Build a (possibly multi-component) opaque partition-key handle.
        /// Each component is added via the typed
        /// <c>cosmos_partition_key_builder_add_*</c> entry point matching
        /// its CLR type. The builder is consumed by
        /// <c>cosmos_partition_key_builder_build</c> on the success path
        /// and freed inline if any add/build step fails.
        /// </summary>
        private static IntPtr BuildPartitionKey(object[] components)
        {
            if (components is null || components.Length == 0)
            {
                throw new ArgumentException(
                    "Partition key needs at least one component.", nameof(components));
            }

            IntPtr pkBuilder = cosmos_partition_key_builder_new();
            if (pkBuilder == IntPtr.Zero)
            {
                throw new InvalidOperationException("cosmos_partition_key_builder_new returned NULL");
            }
            try
            {
                foreach (object? component in components)
                {
                    CosmosErrorCode addRc = component switch
                    {
                        null => cosmos_partition_key_builder_add_null(pkBuilder),
                        string s => cosmos_partition_key_builder_add_string(pkBuilder, s),
                        bool b => cosmos_partition_key_builder_add_bool(pkBuilder, b),
                        double d => cosmos_partition_key_builder_add_number(pkBuilder, d),
                        float f => cosmos_partition_key_builder_add_number(pkBuilder, f),
                        int i => cosmos_partition_key_builder_add_number(pkBuilder, i),
                        long l => cosmos_partition_key_builder_add_number(pkBuilder, l),
                        _ => throw new ArgumentException(
                            $"Unsupported partition-key component type {component.GetType()}", nameof(components)),
                    };
                    ThrowOn(addRc, $"cosmos_partition_key_builder_add_* for {component?.GetType().Name ?? "null"}");
                }

                CosmosErrorCode buildRc = cosmos_partition_key_builder_build(pkBuilder, out IntPtr pk);
                pkBuilder = IntPtr.Zero;  // consumed by build per header §1499
                ThrowOn(buildRc, "cosmos_partition_key_builder_build");
                return pk;
            }
            finally
            {
                if (pkBuilder != IntPtr.Zero) cosmos_partition_key_builder_free(pkBuilder);
            }
        }

        /// <summary>
        /// Resolves a partition-key handle into a single-partition feed range
        /// against this client's container, via
        /// <c>cosmos_feed_range_for_partition_key</c>. This is the path the
        /// driver actually honors for scoping a <c>QueryItems</c> operation —
        /// its <c>build_operation</c> reads only <c>req.feed_range</c> for
        /// queries and never <c>req.partition_key</c> (op_request.rs:685). The
        /// returned handle must be freed with <c>cosmos_feed_range_free</c>.
        /// </summary>
        private IntPtr BuildFeedRangeForPartitionKey(IntPtr pk)
        {
            CosmosErrorCode rc = cosmos_feed_range_for_partition_key(this.container, pk, out IntPtr fr);
            ThrowOn(rc, "cosmos_feed_range_for_partition_key");
            return fr;
        }

        public CompletionQueueLoop CompletionQueue => this.cq;

        /// <summary>
        /// True when this client was constructed via
        /// <see cref="CreateCrossPartition"/> — i.e. it has no pinned
        /// partition key and can only serve cross-partition queries.
        /// </summary>
        public bool IsCrossPartition => this.partitionKey == IntPtr.Zero;

        public Task<CosmosNativeResponse> ReadItemAsync(string itemId, CancellationToken ct = default)
        {
            this.RequirePinnedPartitionKey(nameof(ReadItemAsync));
            return this.RunRequestAsync(b => b
                .WithKind(CosmosOperationKind.ReadItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId), OperationBucket.Singleton, ct);
        }

        public Task<CosmosNativeResponse> CreateItemAsync(string itemId, string bodyJson, CancellationToken ct = default)
        {
            this.RequirePinnedPartitionKey(nameof(CreateItemAsync));
            return this.RunRequestAsync(b => b
                .WithKind(CosmosOperationKind.CreateItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), OperationBucket.Singleton, ct);
        }

        public Task<CosmosNativeResponse> UpsertItemAsync(string itemId, string bodyJson, CancellationToken ct = default)
        {
            this.RequirePinnedPartitionKey(nameof(UpsertItemAsync));
            return this.RunRequestAsync(b => b
                .WithKind(CosmosOperationKind.UpsertItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), OperationBucket.Singleton, ct);
        }

        public Task<CosmosNativeResponse> ReplaceItemAsync(string itemId, string bodyJson, CancellationToken ct = default)
        {
            this.RequirePinnedPartitionKey(nameof(ReplaceItemAsync));
            return this.RunRequestAsync(b => b
                .WithKind(CosmosOperationKind.ReplaceItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId)
                .WithJsonBody(bodyJson), OperationBucket.Singleton, ct);
        }

        public Task<CosmosNativeResponse> DeleteItemAsync(string itemId, CancellationToken ct = default)
        {
            this.RequirePinnedPartitionKey(nameof(DeleteItemAsync));
            return this.RunRequestAsync(b => b
                .WithKind(CosmosOperationKind.DeleteItem)
                .WithContainer(this.container)
                .WithPartitionKey(this.partitionKey)
                .WithItemId(itemId), OperationBucket.Singleton, ct);
        }

        private void RequirePinnedPartitionKey(string memberName)
        {
            if (this.partitionKey == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"{memberName} requires a pinned partition key. Construct " +
                    "NativeCosmosClient with a partition-key value (single or HPK) " +
                    "instead of using CreateCrossPartition for CRUD.");
            }
        }

        /// <summary>
        /// Issue a single page of a SQL query against the container's
        /// fixed partition key. To walk all pages, either loop while
        /// <see cref="CosmosNativeResponse.NextContinuation"/> is non-null
        /// (passing it back as <paramref name="continuationToken"/>), or
        /// use <see cref="QueryItemsAsync"/>.
        /// </summary>
        /// <param name="queryText">SQL query text (e.g. <c>"SELECT * FROM c WHERE c.tag = @tag"</c>).</param>
        /// <param name="continuationToken">
        /// NULL on the first call; for subsequent pages, pass the value
        /// of <see cref="CosmosNativeResponse.NextContinuation"/> from
        /// the previous page.
        /// </param>
        /// <param name="maxItemCount">
        /// Page-size hint. NULL leaves the field unset
        /// (<c>MaxItemCount = -1</c>), letting the driver pick a default.
        /// </param>
        /// <param name="parameters">
        /// Optional parameters for the query body
        /// (e.g. <c>[("@tag", "abc")]</c>). Pass NULL or empty for an
        /// unparameterized query.
        /// </param>
        public Task<CosmosNativeResponse> QueryItemsPageAsync(
            string queryText,
            string? continuationToken = null,
            int? maxItemCount = null,
            IReadOnlyList<(string Name, object? Value)>? parameters = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(queryText))
            {
                throw new ArgumentException("Query text must be non-empty.", nameof(queryText));
            }

            byte[] body = QueryBodyBuilder.Build(queryText, parameters);
            // The driver scopes QueryItems by feed_range, not partition_key
            // (op_request.rs:685 reads only req.feed_range). Resolve the
            // pinned PK into a single-partition feed range; NULL pinned PK =
            // whole-container fan-out (leave feed_range unset).
            IntPtr feedRange = (this.partitionKey != IntPtr.Zero)
                ? BuildFeedRangeForPartitionKey(this.partitionKey)
                : IntPtr.Zero;
            try
            {
                return this.RunRequestAsync(b =>
                {
                    b.WithKind(CosmosOperationKind.QueryItems)
                     .WithContainer(this.container)
                     .WithBody(body);
                    if (feedRange != IntPtr.Zero)
                    {
                        // Scopes the query to the pinned PK's single logical
                        // partition. When omitted, the driver fans out across
                        // all physical partitions (cross-partition query).
                        b.WithFeedRange(feedRange);
                    }
                    if (!string.IsNullOrEmpty(continuationToken))
                    {
                        b.WithContinuationToken(continuationToken);
                    }
                    if (maxItemCount.HasValue)
                    {
                        b.WithMaxItemCount(maxItemCount.Value);
                    }
                }, OperationBucket.Feed, ct);
            }
            finally
            {
                // Safe to free here: RunRequestAsync has run the configure
                // delegate and the synchronous submit (which deep-copies the
                // borrowed feed range) before returning the Task.
                if (feedRange != IntPtr.Zero)
                {
                    cosmos_feed_range_free(feedRange);
                }
            }
        }

        /// <summary>
        /// Walks every page of a SQL query against the container's fixed
        /// partition key, threading the planner-derived continuation
        /// token internally. Yields one
        /// <see cref="CosmosNativeResponse"/> per page; iteration ends
        /// when the driver signals end-of-stream (NULL
        /// <see cref="CosmosNativeResponse.NextContinuation"/>).
        /// </summary>
        public async IAsyncEnumerable<CosmosNativeResponse> QueryItemsAsync(
            string queryText,
            int? maxItemCount = null,
            IReadOnlyList<(string Name, object? Value)>? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            string? continuation = null;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                CosmosNativeResponse page = await this.QueryItemsPageAsync(
                    queryText, continuation, maxItemCount, parameters, ct).ConfigureAwait(false);

                // Degenerate end-of-stream pages (status code 0, NULL next
                // token per header §1685) carry no useful data; skip them
                // rather than surfacing an empty page to the caller.
                if (page.HttpStatusCode != 0)
                {
                    yield return page;
                }

                if (string.IsNullOrEmpty(page.NextContinuation))
                {
                    yield break;
                }
                continuation = page.NextContinuation;
            }
        }

        /// <summary>
        /// Per-request query page — the real-world shape where the partition
        /// key is an ARGUMENT to the call, not pinned at client construction.
        ///
        /// <paramref name="partitionKey"/> semantics:
        ///   * non-empty (e.g. <c>["user-1"]</c> single, or
        ///     <c>["tenant-A","region-east","user-1"]</c> hierarchical) — the
        ///     query is scoped to that one logical partition; the driver should
        ///     route to a single physical partition and return only that
        ///     partition's matching documents.
        ///   * <c>null</c> or empty — the query is a cross-partition fan-out;
        ///     the driver visits every physical partition and merges results.
        ///
        /// The opaque <c>cosmos_partition_key_t*</c> is built on entry and freed
        /// once the synchronous submit returns. The wrapper deep-copies every
        /// borrowed request pointer (including the partition key) before submit
        /// returns (lifetime contract above on <see cref="RunRequestAsync"/>),
        /// so a per-call key has no lifetime hazard.
        /// </summary>
        public Task<CosmosNativeResponse> QueryPageAsync(
            string queryText,
            object[]? partitionKey,
            string? continuationToken = null,
            int? maxItemCount = null,
            IReadOnlyList<(string Name, object? Value)>? parameters = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(queryText))
            {
                throw new ArgumentException("Query text must be non-empty.", nameof(queryText));
            }

            byte[] body = QueryBodyBuilder.Build(queryText, parameters);
            // Build the per-request PK only as an intermediate: the driver
            // scopes QueryItems by feed_range, not partition_key
            // (op_request.rs:685 reads only req.feed_range). Resolve the key
            // into a single-partition feed range; no key = cross-partition.
            IntPtr pk = (partitionKey is { Length: > 0 })
                ? BuildPartitionKey(partitionKey)
                : IntPtr.Zero;
            IntPtr feedRange = (pk != IntPtr.Zero)
                ? BuildFeedRangeForPartitionKey(pk)
                : IntPtr.Zero;
            try
            {
                return this.RunRequestAsync(b =>
                {
                    b.WithKind(CosmosOperationKind.QueryItems)
                     .WithContainer(this.container)
                     .WithBody(body);
                    if (feedRange != IntPtr.Zero)
                    {
                        b.WithFeedRange(feedRange);
                    }
                    if (!string.IsNullOrEmpty(continuationToken))
                    {
                        b.WithContinuationToken(continuationToken);
                    }
                    if (maxItemCount.HasValue)
                    {
                        b.WithMaxItemCount(maxItemCount.Value);
                    }
                }, OperationBucket.Feed, ct);
            }
            finally
            {
                // Safe to free here: RunRequestAsync has already run the
                // configure delegate and the synchronous submit (which
                // deep-copies the borrowed handles) before returning the Task.
                if (feedRange != IntPtr.Zero)
                {
                    cosmos_feed_range_free(feedRange);
                }
                if (pk != IntPtr.Zero)
                {
                    cosmos_partition_key_free(pk);
                }
            }
        }

        /// <summary>
        /// Walks every page of a per-request query (see
        /// <see cref="QueryPageAsync"/>), threading the planner-derived
        /// continuation token internally. The same <paramref name="partitionKey"/>
        /// is applied to every page; pass <c>null</c> for a cross-partition
        /// fan-out.
        /// </summary>
        public async IAsyncEnumerable<CosmosNativeResponse> QueryPagesAsync(
            string queryText,
            object[]? partitionKey,
            int? maxItemCount = null,
            IReadOnlyList<(string Name, object? Value)>? parameters = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            string? continuation = null;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                CosmosNativeResponse page = await this.QueryPageAsync(
                    queryText, partitionKey, continuation, maxItemCount, parameters, ct).ConfigureAwait(false);

                if (page.HttpStatusCode != 0)
                {
                    yield return page;
                }

                if (string.IsNullOrEmpty(page.NextContinuation))
                {
                    yield break;
                }
                continuation = page.NextContinuation;
            }
        }

        /// <summary>
        /// Core dispatch path against the post-PR-#4515 spec.
        ///
        /// Builds a <see cref="CosmosOperationRequest"/> via the supplied
        /// <paramref name="configure"/> action, dispatches through either
        /// <c>cosmos_submit_singleton_operation</c> or
        /// <c>cosmos_submit_operation</c> based on
        /// <paramref name="bucket"/>, and wires the resulting opaque
        /// handle into the CQ pump (which completes the TCS once the
        /// Rust side posts a completion).
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
        private Task<CosmosNativeResponse> RunRequestAsync(
            Action<CosmosOperationRequestBuilder> configure,
            OperationBucket bucket,
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
                    bucket, out preError);
            }

            if (opHandle == IntPtr.Zero)
            {
                // Pre-flight rollback on the Rust side: no completion will
                // arrive, so we own the GCHandle free.
                GCHandle.FromIntPtr(userData).Free();
                op.Tcs.TrySetException(new InvalidOperationException(
                    $"cosmos_driver_execute_{(bucket == OperationBucket.Singleton ? "singleton_" : string.Empty)}operation_submit pre-flight rejected: {preError} (op#{op.Id})"));
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
            if (this.partitionKey != IntPtr.Zero)
            {
                cosmos_partition_key_free(this.partitionKey);
            }
            cosmos_container_ref_free(this.container);
            cosmos_driver_free(this.driver);
            cosmos_database_ref_free(this.database);
            cosmos_account_ref_free(this.account);
            cosmos_runtime_free(this.runtime);
        }
    }
}
