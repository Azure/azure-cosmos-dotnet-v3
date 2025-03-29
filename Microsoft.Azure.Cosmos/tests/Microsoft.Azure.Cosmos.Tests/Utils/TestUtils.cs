//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Moq;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Common utility class for unit tests.
    /// </summary>
    internal static class TestUtils
    {
        /// <summary>
        /// This helper method uses reflection to set the private and read only fields
        /// to the disered values to help the test cases mimic the expected behavior.
        /// </summary>
        /// <param name="objectName">An object where reflection will be applied to update the field.</param>
        /// <param name="fieldName">A string containing the internal field name.</param>
        /// <param name="delayInMinutes">An integer to add or substract the desired delay in minutes.</param>
        internal static void AddMinuteToDateTimeFieldUsingReflection(
            object objectName,
            string fieldName,
            int delayInMinutes)
        {
            FieldInfo fieldInfo = objectName
                .GetType()
                .GetField(
                    name: fieldName,
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            DateTime? fieldValue = (DateTime?)fieldInfo
                .GetValue(
                    obj: objectName);

            fieldInfo
                .SetValue(
                    obj: objectName,
                    value: ((DateTime)fieldValue).AddMinutes(delayInMinutes));
        }

        public static void SetupCachesInGatewayStoreModel(
            GatewayStoreModel storeModel,
            GlobalEndpointManager endpointManager)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Kind = PartitionKind.Hash,
                Paths = new Collection<string>()
                {
                    "/id"
                }
            };

            // Prepare mocked caches.
            Mock<ClientCollectionCache> clientCollectionCache = new Mock<ClientCollectionCache>(new SessionContainer("testhost"), storeModel, null, null, null, false);
            Mock<PartitionKeyRangeCache> partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(null, storeModel, clientCollectionCache.Object, endpointManager, false);

            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId("test");
            containerProperties.PartitionKey = partitionKeyDefinition;
            clientCollectionCache.Setup
                    (m =>
                        m.ResolveCollectionAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<ITrace>()
                    )
                ).Returns(Task.FromResult(containerProperties));

            storeModel.SetCaches(partitionKeyRangeCache.Object, clientCollectionCache.Object);
        }
    }
}