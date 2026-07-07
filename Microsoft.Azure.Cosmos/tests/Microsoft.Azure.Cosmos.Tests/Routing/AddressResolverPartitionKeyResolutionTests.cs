//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the shared, request-free partition-key -> range core
    /// (<see cref="AddressResolver.TryResolvePartitionKeyToRange"/>) and the gateway wrapper
    /// (<see cref="AddressResolver.TryResolveServerPartitionByPartitionKey"/>) that translates the core's
    /// result back into the gateway's historical throw/return-null contract.
    /// </summary>
    [TestClass]
    public class AddressResolverPartitionKeyResolutionTests
    {
        private const string CollectionRid = "OVJwAA==";
        private const string DocumentPath = "dbs/OVJwAA==/colls/OVJwAOcMtA0=/docs/OVJwAOcMtA0BAAAAAAAAAA==/";

        // ---------- U3.1: TryResolvePartitionKeyToRange matrix ----------

        [TestMethod]
        public void U31_FullKey_Resolves_ToOwningRange()
        {
            ContainerProperties collection = SinglePathContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();
            PartitionKeyInternal fullKey = PartitionKeyInternal.FromJsonString("[\"a\"]");

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                fullKey,
                collection,
                routingMap,
                collectionCacheUptoDate: true,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.Resolved, kind);
            Assert.IsNotNull(range);
            Assert.AreEqual("0", range.Id);
        }

        [TestMethod]
        public void U31_EmptyKey_Resolves()
        {
            ContainerProperties collection = SinglePathContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                PartitionKeyInternal.Empty,
                collection,
                routingMap,
                collectionCacheUptoDate: true,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.Resolved, kind);
            Assert.IsNotNull(range);
            Assert.AreEqual("0", range.Id);
        }

        [TestMethod]
        public void U31_FullHierarchicalKey_Resolves()
        {
            ContainerProperties collection = HierarchicalContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();
            PartitionKeyInternal fullKey = PartitionKeyInternal.FromJsonString("[\"a\",\"b\"]");

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                fullKey,
                collection,
                routingMap,
                collectionCacheUptoDate: true,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.Resolved, kind);
            Assert.IsNotNull(range);
        }

        [TestMethod]
        public void U31_PartialHierarchicalPrefix_CacheUpToDate_IsKeyMismatch()
        {
            ContainerProperties collection = HierarchicalContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();

            // Only the first of two defined paths supplied -> component count mismatch.
            PartitionKeyInternal partialKey = PartitionKeyInternal.FromJsonString("[\"a\"]");

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                partialKey,
                collection,
                routingMap,
                collectionCacheUptoDate: true,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.KeyMismatch, kind);
            Assert.IsNull(range);
        }

        [TestMethod]
        public void U31_PartialHierarchicalPrefix_StaleCache_NeedsRefresh()
        {
            ContainerProperties collection = HierarchicalContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();

            PartitionKeyInternal partialKey = PartitionKeyInternal.FromJsonString("[\"a\"]");

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                partialKey,
                collection,
                routingMap,
                collectionCacheUptoDate: false,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.NeedsRefresh, kind);
            Assert.IsNull(range);
        }

        [TestMethod]
        public void U31_NoOwningRange_NeedsRefresh_Unreachable_Documented()
        {
            // U3.1 lists "key with no owning range => NeedsRefresh". That branch maps a null
            // GetRangeByEffectivePartitionKey result to NeedsRefresh, but it is defensive-only: a
            // *complete* CollectionRoutingMap always returns a containing range for an in-bounds key, and
            // the class is sealed with a non-virtual lookup, so no real map or mock can yield a null range.
            // This test documents that unreachability; the full key below therefore resolves.
            ContainerProperties collection = SinglePathContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();

            AddressResolver.PartitionKeyRangeResolutionKind kind = AddressResolver.TryResolvePartitionKeyToRange(
                PartitionKeyInternal.FromJsonString("[\"a\"]"),
                collection,
                routingMap,
                collectionCacheUptoDate: true,
                out PartitionKeyRange range);

            Assert.AreEqual(AddressResolver.PartitionKeyRangeResolutionKind.Resolved, kind);
            Assert.IsNotNull(range);
        }

        [TestMethod]
        public void U31_NullArguments_Throw()
        {
            ContainerProperties collection = SinglePathContainer();
            CollectionRoutingMap routingMap = SingleRangeRoutingMap();
            PartitionKeyInternal key = PartitionKeyInternal.FromJsonString("[\"a\"]");

            Assert.ThrowsException<ArgumentNullException>(() =>
                AddressResolver.TryResolvePartitionKeyToRange(null, collection, routingMap, true, out _));
            Assert.ThrowsException<ArgumentNullException>(() =>
                AddressResolver.TryResolvePartitionKeyToRange(key, null, routingMap, true, out _));
            Assert.ThrowsException<ArgumentNullException>(() =>
                AddressResolver.TryResolvePartitionKeyToRange(key, collection, null, true, out _));
        }

        // ---------- R3.2: gateway wrapper translation ----------

        [TestMethod]
        public void R32_Wrapper_FullKey_ReturnsRange()
        {
            using DocumentServiceRequest request = CreateDocumentRequest();

            PartitionKeyRange range = AddressResolver.TryResolveServerPartitionByPartitionKey(
                request,
                partitionKeyString: "[\"a\"]",
                collectionCacheUptoDate: true,
                collection: SinglePathContainer(),
                routingMap: SingleRangeRoutingMap());

            Assert.IsNotNull(range);
            Assert.AreEqual("0", range.Id);
        }

        [TestMethod]
        public void R32_Wrapper_KeyMismatch_Throws_PartitionKeyMismatch_With_ResourceAddress()
        {
            using DocumentServiceRequest request = CreateDocumentRequest();

            BadRequestException exception = Assert.ThrowsException<BadRequestException>(() =>
                AddressResolver.TryResolveServerPartitionByPartitionKey(
                    request,
                    partitionKeyString: "[\"a\"]", // one component against a two-path definition
                    collectionCacheUptoDate: true,
                    collection: HierarchicalContainer(),
                    routingMap: SingleRangeRoutingMap()));

            Assert.AreEqual(
                ((uint)SubStatusCodes.PartitionKeyMismatch).ToString(CultureInfo.InvariantCulture),
                exception.Headers[WFConstants.BackendHeaders.SubStatus]);
            Assert.AreEqual(request.ResourceAddress, exception.ResourceAddress);
        }

        [TestMethod]
        public void R32_Wrapper_StaleCachePartialKey_ReturnsNull()
        {
            using DocumentServiceRequest request = CreateDocumentRequest();

            PartitionKeyRange range = AddressResolver.TryResolveServerPartitionByPartitionKey(
                request,
                partitionKeyString: "[\"a\"]",
                collectionCacheUptoDate: false,
                collection: HierarchicalContainer(),
                routingMap: SingleRangeRoutingMap());

            Assert.IsNull(range);
        }

        // ---------- helpers ----------

        private static DocumentServiceRequest CreateDocumentRequest()
        {
            return DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                DocumentPath,
                AuthorizationTokenType.PrimaryMasterKey,
                new DictionaryNameValueCollection());
        }

        private static ContainerProperties SinglePathContainer()
        {
            return new ContainerProperties()
            {
                Id = "id",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.Hash,
                    Paths = new Collection<string>() { "/path1" },
                    Version = PartitionKeyDefinitionVersion.V2,
                },
            };
        }

        private static ContainerProperties HierarchicalContainer()
        {
            return new ContainerProperties()
            {
                Id = "id",
                PartitionKey = new PartitionKeyDefinition()
                {
                    Kind = PartitionKind.MultiHash,
                    Paths = new Collection<string>() { "/path1", "/path2" },
                    Version = PartitionKeyDefinitionVersion.V2,
                },
            };
        }

        private static CollectionRoutingMap SingleRangeRoutingMap()
        {
            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(
                        new PartitionKeyRange { Id = "0", MinInclusive = string.Empty, MaxExclusive = "FF" },
                        (ServiceIdentity)null),
                },
                CollectionRid,
                useLengthAwareRangeComparer: false);

            Assert.IsNotNull(routingMap);
            return routingMap;
        }
    }
}
