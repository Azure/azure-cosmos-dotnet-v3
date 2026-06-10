// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using static Microsoft.Azure.Cosmos.NativeDriverPoc.NativeMethods;

    /// <summary>
    /// Safe construction site for <see cref="CosmosOperationRequest"/> as
    /// defined by the PR #4515 redesign (header lines 850-933).
    ///
    /// The request struct carries six borrowed UTF-8 strings, one borrowed
    /// byte buffer (the body), and a pointer to an optional sub-struct
    /// (<see cref="CosmosOperationOptions"/>) that itself can carry more
    /// borrowed strings / arrays. Every one of those pointers must remain
    /// valid for the duration of the single
    /// <c>cosmos_driver_execute_*_submit</c> call; the wrapper deep-copies
    /// before returning, so the caller may free immediately after submit.
    ///
    /// This builder centralizes the lifetime contract:
    ///   1. Each <c>With…</c> setter takes a managed value and stages it
    ///      as an unmanaged allocation owned by this builder.
    ///   2. <see cref="Submit"/> assembles the
    ///      <see cref="CosmosOperationRequest"/>, dispatches to the
    ///      appropriate FFI entry point, and returns the operation handle
    ///      + the pre-flight error code.
    ///   3. <see cref="Dispose"/> releases every staged allocation in
    ///      reverse order.
    /// </summary>
    /// <remarks>
    /// Strings are copied into CoTaskMem buffers (UTF-8, NUL-terminated)
    /// because <c>Marshal.StringToCoTaskMemUTF8</c> is the simplest API
    /// that yields a stable <see cref="IntPtr"/> for the lifetime of the
    /// call. The body is pinned in place via <see cref="GCHandle"/> with
    /// <c>GCHandleType.Pinned</c>, which avoids a managed→unmanaged copy
    /// for what is typically the largest payload.
    /// </remarks>
    internal sealed class CosmosOperationRequestBuilder : IDisposable
    {
        private readonly List<IntPtr> coTaskMemAllocations = new(8);
        private readonly List<GCHandle> pinnedHandles = new(2);

        private CosmosOperationKind kind = CosmosOperationKind.Invalid;
        private IntPtr account;
        private IntPtr database;
        private IntPtr container;
        private IntPtr partitionKey;
        private IntPtr feedRange;
        private IntPtr itemIdPtr;
        private IntPtr resourceLinkPtr;
        private IntPtr sessionTokenPtr;
        private IntPtr activityIdPtr;
        private IntPtr continuationTokenPtr;
        private IntPtr preconditionEtagPtr;
        private CosmosPreconditionKind preconditionKind = CosmosPreconditionKind.None;
        private CosmosBytesView body = CosmosBytesView.Empty;
        private int maxItemCount = -1;
        private byte patchMaxAttempts;
        private sbyte populateIndexMetrics;
        private sbyte populateQueryMetrics;

        // Options sub-struct is opt-in. We hold it as a 1-element array
        // (rather than nullable struct) so WithOptions(ref ...) can return
        // a stable ref the caller can mutate and Submit can pin in place.
        private CosmosOperationOptions[]? operationOptions;

        public CosmosOperationRequestBuilder WithKind(CosmosOperationKind value)
        {
            this.kind = value;
            return this;
        }

        public CosmosOperationRequestBuilder WithAccount(IntPtr handle)
        {
            this.account = handle;
            return this;
        }

        public CosmosOperationRequestBuilder WithDatabase(IntPtr handle)
        {
            this.database = handle;
            return this;
        }

        public CosmosOperationRequestBuilder WithContainer(IntPtr handle)
        {
            this.container = handle;
            return this;
        }

        public CosmosOperationRequestBuilder WithPartitionKey(IntPtr handle)
        {
            this.partitionKey = handle;
            return this;
        }

        public CosmosOperationRequestBuilder WithFeedRange(IntPtr handle)
        {
            this.feedRange = handle;
            return this;
        }

        public CosmosOperationRequestBuilder WithItemId(string? value)
        {
            this.itemIdPtr = this.StageUtf8(value);
            return this;
        }

        public CosmosOperationRequestBuilder WithResourceLink(string? value)
        {
            this.resourceLinkPtr = this.StageUtf8(value);
            return this;
        }

        public CosmosOperationRequestBuilder WithSessionToken(string? value)
        {
            this.sessionTokenPtr = this.StageUtf8(value);
            return this;
        }

        public CosmosOperationRequestBuilder WithActivityId(string? value)
        {
            this.activityIdPtr = this.StageUtf8(value);
            return this;
        }

        public CosmosOperationRequestBuilder WithContinuationToken(string? value)
        {
            this.continuationTokenPtr = this.StageUtf8(value);
            return this;
        }

        public CosmosOperationRequestBuilder WithPrecondition(CosmosPreconditionKind kind, string? etag)
        {
            this.preconditionKind = kind;
            this.preconditionEtagPtr = this.StageUtf8(etag);
            return this;
        }

        public CosmosOperationRequestBuilder WithMaxItemCount(int value)
        {
            this.maxItemCount = value;
            return this;
        }

        public CosmosOperationRequestBuilder WithPatchMaxAttempts(byte value)
        {
            this.patchMaxAttempts = value;
            return this;
        }

        public CosmosOperationRequestBuilder WithPopulateIndexMetrics(bool? value) =>
            this.WithTriStateBool(value, ref this.populateIndexMetrics);

        public CosmosOperationRequestBuilder WithPopulateQueryMetrics(bool? value) =>
            this.WithTriStateBool(value, ref this.populateQueryMetrics);

        public CosmosOperationRequestBuilder WithJsonBody(string? body)
        {
            if (string.IsNullOrEmpty(body))
            {
                this.body = CosmosBytesView.Empty;
                return this;
            }
            return this.WithBody(Encoding.UTF8.GetBytes(body));
        }

        public CosmosOperationRequestBuilder WithBody(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0)
            {
                this.body = CosmosBytesView.Empty;
                return this;
            }
            GCHandle pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            this.pinnedHandles.Add(pin);
            this.body = new CosmosBytesView
            {
                Data = pin.AddrOfPinnedObject(),
                Len = (UIntPtr)bytes.Length,
            };
            return this;
        }

        /// <summary>
        /// Stage a per-call options struct. The first call seeds it from
        /// <see cref="cosmos_operation_options_default"/>; subsequent
        /// calls return a stable <see cref="ref"/> into the same backing
        /// storage so the caller can keep mutating. The struct is pinned
        /// in place at <see cref="Submit"/> time.
        /// </summary>
        public ref CosmosOperationOptions WithOptions()
        {
            if (this.operationOptions is null)
            {
                this.operationOptions = new CosmosOperationOptions[1];
                this.operationOptions[0] = cosmos_operation_options_default();
            }
            return ref this.operationOptions[0];
        }

        /// <summary>
        /// Build the request, pin the options sub-struct (if any), and
        /// dispatch through one of the two submit entry points based on
        /// <paramref name="bucket"/>. Returns the
        /// <c>cosmos_operation_handle_t*</c> + pre-flight error code.
        /// On a NULL handle the caller should treat
        /// <paramref name="preError"/> as the failure code.
        /// </summary>
        public IntPtr Submit(
            IntPtr driver,
            IntPtr queue,
            IntPtr userData,
            OperationBucket bucket,
            out CosmosErrorCode preError)
        {
            if (this.kind == CosmosOperationKind.Invalid)
            {
                throw new InvalidOperationException(
                    "CosmosOperationRequestBuilder.Submit called without WithKind(...)");
            }

            // Stage the options sub-struct if the caller mutated it.
            IntPtr optsPtr = IntPtr.Zero;
            if (this.operationOptions is not null)
            {
                GCHandle optsPin = GCHandle.Alloc(this.operationOptions, GCHandleType.Pinned);
                this.pinnedHandles.Add(optsPin);
                optsPtr = optsPin.AddrOfPinnedObject();
            }

            CosmosOperationRequest request = new CosmosOperationRequest
            {
                Kind = this.kind,
                Account = this.account,
                Database = this.database,
                Container = this.container,
                ItemId = this.itemIdPtr,
                ResourceLink = this.resourceLinkPtr,
                PartitionKey = this.partitionKey,
                FeedRange = this.feedRange,
                Body = this.body,
                SessionToken = this.sessionTokenPtr,
                ActivityId = this.activityIdPtr,
                ContinuationToken = this.continuationTokenPtr,
                MaxItemCount = this.maxItemCount,
                PatchMaxAttempts = this.patchMaxAttempts,
                PopulateIndexMetrics = this.populateIndexMetrics,
                PopulateQueryMetrics = this.populateQueryMetrics,
                PreconditionKind = this.preconditionKind,
                PreconditionEtag = this.preconditionEtagPtr,
                Options = optsPtr,
            };

            return bucket switch
            {
                OperationBucket.Singleton => cosmos_driver_execute_singleton_operation_submit(
                    driver, in request, queue, userData, out preError),
                OperationBucket.Feed => cosmos_driver_execute_operation_submit(
                    driver, in request, queue, userData, out preError),
                _ => throw new ArgumentOutOfRangeException(nameof(bucket)),
            };
        }

        /// <summary>
        /// Routing classifier — every <see cref="CosmosOperationKind"/>
        /// goes to exactly one of these. Mirrors the spec's split between
        /// <c>execute_singleton_operation</c> and <c>execute_operation</c>.
        /// </summary>
        public static OperationBucket BucketFor(CosmosOperationKind kind) => kind switch
        {
            // Feed (paginated, threads continuation_token)
            CosmosOperationKind.ReadAllDatabases => OperationBucket.Feed,
            CosmosOperationKind.QueryDatabases => OperationBucket.Feed,
            CosmosOperationKind.QueryOffers => OperationBucket.Feed,
            CosmosOperationKind.ReadAllContainers => OperationBucket.Feed,
            CosmosOperationKind.QueryContainers => OperationBucket.Feed,
            CosmosOperationKind.ReadAllItems => OperationBucket.Feed,
            CosmosOperationKind.ReadAllItemsCrossPartition => OperationBucket.Feed,
            CosmosOperationKind.QueryItems => OperationBucket.Feed,

            // Everything else returns exactly one result.
            _ => OperationBucket.Singleton,
        };

        public void Dispose()
        {
            // Free pinned handles first (they may reference managed objects).
            for (int i = this.pinnedHandles.Count - 1; i >= 0; i--)
            {
                if (this.pinnedHandles[i].IsAllocated)
                {
                    this.pinnedHandles[i].Free();
                }
            }
            this.pinnedHandles.Clear();

            // Free the CoTaskMem-allocated UTF-8 strings.
            for (int i = this.coTaskMemAllocations.Count - 1; i >= 0; i--)
            {
                IntPtr p = this.coTaskMemAllocations[i];
                if (p != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(p);
                }
            }
            this.coTaskMemAllocations.Clear();
        }

        private IntPtr StageUtf8(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }
            IntPtr p = Marshal.StringToCoTaskMemUTF8(value);
            this.coTaskMemAllocations.Add(p);
            return p;
        }

        private CosmosOperationRequestBuilder WithTriStateBool(bool? value, ref sbyte field)
        {
            field = value switch
            {
                null => TristateUnset,
                false => TristateFalse,
                true => TristateTrue,
            };
            return this;
        }
    }

    /// <summary>
    /// Selects which of the two submit entry points to dispatch through.
    /// </summary>
    internal enum OperationBucket
    {
        /// <summary>Point op / single-result — calls <c>cosmos_driver_execute_singleton_operation_submit</c>.</summary>
        Singleton,
        /// <summary>Paginated / feed — calls <c>cosmos_driver_execute_operation_submit</c>.</summary>
        Feed,
    }
}
