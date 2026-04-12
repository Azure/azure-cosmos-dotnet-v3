//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DocumentClientDisposeTests
    {
        /// <summary>
        /// Verifies that DocumentClient.Dispose() disposes PartitionKeyRangeLocation
        /// when the implementation is IDisposable (GlobalPartitionEndpointManagerCore)
        /// and sets it to null.
        /// Regression test for: https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5777
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void Dispose_DisposesPartitionKeyRangeLocationWhenIDisposable()
        {
            using MockDocumentClient documentClient = new MockDocumentClient();

            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: false,
                isPartitionLevelCircuitBreakerEnabled: false);

            DocumentClientDisposeTests.SetPartitionKeyRangeLocation(documentClient, manager);
            Assert.IsNotNull(documentClient.PartitionKeyRangeLocation);
            Assert.IsInstanceOfType(documentClient.PartitionKeyRangeLocation, typeof(IDisposable));

            documentClient.Dispose();

            Assert.IsNull(documentClient.PartitionKeyRangeLocation, "PartitionKeyRangeLocation should be null after Dispose.");

            // Verify the manager was actually disposed: after disposal the cancellation token
            // is cancelled, so re-initialization of the background loop is a no-op.
            manager.InitializeAndStartCircuitBreakerFailbackBackgroundRefresh();
        }

        /// <summary>
        /// Verifies that DocumentClient.Dispose() does not throw when
        /// PartitionKeyRangeLocation does not implement IDisposable (GlobalPartitionEndpointManagerNoOp).
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void Dispose_HandlesNonDisposablePartitionKeyRangeLocation()
        {
            using MockDocumentClient documentClient = new MockDocumentClient();

            DocumentClientDisposeTests.SetPartitionKeyRangeLocation(documentClient, GlobalPartitionEndpointManagerNoOp.Instance);
            Assert.IsNotNull(documentClient.PartitionKeyRangeLocation);
            Assert.IsFalse(documentClient.PartitionKeyRangeLocation is IDisposable);

            // Should not throw.
            documentClient.Dispose();

            // Non-disposable implementation should remain unchanged (not nulled).
            Assert.IsNotNull(documentClient.PartitionKeyRangeLocation);
        }

        /// <summary>
        /// Verifies that DocumentClient.Dispose() does not throw when
        /// PartitionKeyRangeLocation is null (e.g. client never fully initialized).
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void Dispose_HandlesNullPartitionKeyRangeLocation()
        {
            using MockDocumentClient documentClient = new MockDocumentClient();

            Assert.IsNull(documentClient.PartitionKeyRangeLocation);

            // Should not throw.
            documentClient.Dispose();
        }

        /// <summary>
        /// Verifies that calling DocumentClient.Dispose() multiple times is safe
        /// when PartitionKeyRangeLocation is IDisposable.
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public void Dispose_IsIdempotentWithDisposablePartitionKeyRangeLocation()
        {
            using MockDocumentClient documentClient = new MockDocumentClient();

            Mock<IGlobalEndpointManager> mockEndpointManager = new Mock<IGlobalEndpointManager>(MockBehavior.Loose);
            GlobalPartitionEndpointManagerCore manager = new GlobalPartitionEndpointManagerCore(
                mockEndpointManager.Object,
                isPartitionLevelFailoverEnabled: true);

            DocumentClientDisposeTests.SetPartitionKeyRangeLocation(documentClient, manager);

            // First dispose should dispose the manager and null the property.
            documentClient.Dispose();
            Assert.IsNull(documentClient.PartitionKeyRangeLocation);

            // Second dispose should not throw (idempotent).
            documentClient.Dispose();
        }

        private static void SetPartitionKeyRangeLocation(DocumentClient client, GlobalPartitionEndpointManager value)
        {
            PropertyInfo property = typeof(DocumentClient).GetProperty(
                nameof(DocumentClient.PartitionKeyRangeLocation),
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(property, "Could not find PartitionKeyRangeLocation property on DocumentClient.");
            property.SetValue(client, value);
        }
    }
}
