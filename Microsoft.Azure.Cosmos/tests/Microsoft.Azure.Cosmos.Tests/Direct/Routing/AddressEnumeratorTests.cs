//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using TestUtils = Microsoft.Azure.Cosmos.Tests.TestUtils;

    [TestClass]
    public class AddressEnumeratorTests
    {
        [TestMethod]
        [Owner("dikshibahl")]
        public void AddressEnumeratorEmptyListTest()
        {
            List<TransportAddressUri> uris = new List<TransportAddressUri>();
            AddressEnumerator addressEnumerator = new AddressEnumerator();
            IEnumerable<TransportAddressUri> iter = addressEnumerator.GetTransportAddresses(uris, new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(), false);
            Assert.IsFalse(iter.Any());
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddressEnumerator_throws_ArgumentNullException_on_null_UriList()
        {
            AddressEnumerator addressEnumerator = new AddressEnumerator();
            addressEnumerator.GetTransportAddresses(null, new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(), false).ToList();
        }

        [TestMethod]
        [Owner("askagarw")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddressEnumerator_throws_ArgumentNullException_on_null_FailedReplicas()
        {
            AddressEnumerator addressEnumerator = new AddressEnumerator();
            List<TransportAddressUri> uris = new List<TransportAddressUri>();
            addressEnumerator.GetTransportAddresses(uris, null, false).ToList();
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(1)]
        [DataRow(3)]
        [DataRow(5)]
        [DataRow(8)]
        [DataRow(10)]
        [DataRow(100)]
        [DataRow(1000)]
        public void AddressEnumeratorReturnsDistinctListTest(int countOfUris)
        {
            List<TransportAddressUri> uris = new List<TransportAddressUri>(countOfUris);

            for (int i = 0; i < countOfUris; i++)
            {
                uris.Add(new TransportAddressUri(new Uri("https://localhost:" + i)));
            }

            HashSet<TransportAddressUri> transportAddressUrisOutput = new HashSet<TransportAddressUri>();
            foreach (TransportAddressUri transportAddressUri in new AddressEnumerator().GetTransportAddresses(uris, new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(() => new ConcurrentDictionary<TransportAddressUri, bool>()), false))
            {
                Assert.IsTrue(transportAddressUrisOutput.Add(transportAddressUri));
            }

            Assert.AreEqual(uris.Count, transportAddressUrisOutput.Count);
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        public void AddressEnumeratorUsesAllPermutationsTest(int countOfUris)
        {
            List<TransportAddressUri> uris = new List<TransportAddressUri>(countOfUris);

            for (int i = 0; i < countOfUris; i++)
            {
                uris.Add(new TransportAddressUri(new Uri("https://localhost:" + i)));
            }

            // Generate a hashset of all the permutations for the list of URIs
            HashSet<string> allPermutations = new HashSet<string>();
            foreach (IEnumerable<TransportAddressUri> sequence in GetPermutations<TransportAddressUri>(uris))
            {
                allPermutations.Add(string.Join(",", sequence));
            }

            // Restrict the number of iteration to avoid the test from getting stuck
            int iterationCount = 0;
            int maxIterationCount = allPermutations.Count * 50;
            do
            {
                iterationCount++;
                IEnumerator<TransportAddressUri> iter = new AddressEnumerator().GetTransportAddresses(uris, new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(), false).GetEnumerator();

                List<TransportAddressUri> urisFromIterator = new List<TransportAddressUri>();

                // Verify that the uri is never duplicated in the iteration
                HashSet<TransportAddressUri> output = new HashSet<TransportAddressUri>();
                while (iter.MoveNext())
                {
                    urisFromIterator.Add(iter.Current);
                    Assert.IsTrue(output.Add(iter.Current));
                }

                // Remove a permutation once it has been visited. This used to verify all permutations are visited
                allPermutations.Remove(string.Join(",", urisFromIterator));
                Assert.AreEqual(uris.Count, output.Count);
            } while (allPermutations.Any() && iterationCount < maxIterationCount);

            Assert.IsFalse(allPermutations.Any(), $"AddressEnumerator is not random. The following permutation were never used: {string.Join(";", allPermutations)}");
        }

        [TestMethod]
        [Owner("askagarw")]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void AddressEnumeratorWithFailedReplicasTest(int numberOfFailedReplicas)
        {
            int totalUriCount = 5;
            List<TransportAddressUri> uris = new List<TransportAddressUri>(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                uris.Add(new TransportAddressUri(new Uri("https://localhost:" + i)));
            }

            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext();
            for (int i = 0; i < numberOfFailedReplicas; i++)
            {
                requestContext.FailedEndpoints.Value.TryAdd(uris[i], true);
            }

            IEnumerable<TransportAddressUri> addressEnumerable = new AddressEnumerator().GetTransportAddresses(uris, requestContext.FailedEndpoints, false);
            Assert.AreEqual(addressEnumerable.Count(), totalUriCount);
            int currentIndex = 0;
            HashSet<TransportAddressUri> transportAddressUrisOutput = new HashSet<TransportAddressUri>();
            // Test to see all failed address are at the end
            foreach (TransportAddressUri uri in addressEnumerable)
            {
                if (currentIndex < (totalUriCount - numberOfFailedReplicas))
                {
                    Assert.IsFalse(requestContext.FailedEndpoints.Value.ContainsKey(uri));
                }
                else
                {
                    Assert.IsTrue(requestContext.FailedEndpoints.Value.ContainsKey(uri));
                }
                transportAddressUrisOutput.Add(uri);
                currentIndex++;
            }
            Assert.AreEqual(totalUriCount, transportAddressUrisOutput.Count);
        }

        [TestMethod]
        [Owner("askagarw")]
        public void DocumentServiceRequestContext_FailedEnpointsTest()
        {
            DocumentServiceRequestContext requestContext = new DocumentServiceRequestContext();
            Assert.IsFalse(requestContext.FailedEndpoints.IsValueCreated);
            requestContext.AddToFailedEndpoints(new GoneException(), new TransportAddressUri(new Uri("http://someUri1.com")));
            Assert.AreEqual(requestContext.FailedEndpoints.Value.Count, 1);

            // Added to Set only for 410, 408 >= 500
            requestContext.AddToFailedEndpoints(new RetryWithException("RetryWith"), new TransportAddressUri(new Uri("http://someUri2.com")));
            Assert.AreEqual(requestContext.FailedEndpoints.Value.Count, 1);
        }

        [TestMethod]
        [Owner("dikshibahl")]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void AddressEnumeratorWithUnhealthyUriTest(int numberOfFailedReplicas)
        {
            int totalUriCount = 5;
            List<TransportAddressUri> uris = new List<TransportAddressUri>(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                var transportUri = new TransportAddressUri(new Uri("https://localhost:" + i));
                uris.Add(transportUri);
            }

            // Randomly set URI to unhealthy
            foreach (TransportAddressUri uri in uris.OrderBy(x => Guid.NewGuid()).Take(numberOfFailedReplicas))
            {
                uri.SetUnhealthy();
            }

            IEnumerable<TransportAddressUri> addressEnumerable = new AddressEnumerator().GetTransportAddresses(uris, new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(() => new ConcurrentDictionary<TransportAddressUri, bool>()), false);
            Assert.AreEqual(addressEnumerable.Count(), totalUriCount);

            HashSet<TransportAddressUri> transportAddressUrisOutput = new HashSet<TransportAddressUri>();
            // Test to see all failed address are at the end
            foreach (TransportAddressUri uri in addressEnumerable)
            {
                transportAddressUrisOutput.Add(uri);
                if (uri.IsUnhealthy())
                {
                    Assert.IsTrue(transportAddressUrisOutput.Count > uris.Count - numberOfFailedReplicas, "Unheathy URIs should be at the end");
                }
            }

            Assert.AreEqual(totalUriCount, transportAddressUrisOutput.Count);
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task TransportAddressUriUnHealthyTimeoutValidationTest()
        {
            int totalUriCount = 4;
            List<TransportAddressUri> uris = new List<TransportAddressUri>(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                var transportUri = new TransportAddressUri(new Uri("https://localhost:" + i));
                uris.Add(transportUri);
            }

            TransportAddressUri unHealthyUri = uris[0];
            unHealthyUri.SetUnhealthy();
            DateTime timeSetToUnhealthy = DateTime.UtcNow;

            TransportAddressUri failedUri = uris[1];
            Lazy<ConcurrentDictionary<TransportAddressUri, bool>> failedReplicas = new Lazy<ConcurrentDictionary<TransportAddressUri, bool>>(() => new ConcurrentDictionary<TransportAddressUri, bool>());
            failedReplicas.Value.TryAdd(failedUri, true);

            List<TransportAddressUri> addressEnumerable = new AddressEnumerator().GetTransportAddresses(uris, failedReplicas, false).ToList();
            Assert.AreEqual(addressEnumerable.Count, totalUriCount);

            HashSet<TransportAddressUri> transportAddressUrisOutput = new HashSet<TransportAddressUri>(addressEnumerable);

            if (failedUri == addressEnumerable[0]
                || failedUri == addressEnumerable[1])
            {
                Assert.Fail("failedRreplicas should be at the end of the list.");
            }

            if (unHealthyUri == addressEnumerable[0]
                || unHealthyUri == addressEnumerable[1])
            {
                Assert.Fail("Unhealthy replicas should be at the end of the list.");
            }

            TransportAddressUri unHealthyUriFromEnumerable = addressEnumerable[2].IsUnhealthy() ? addressEnumerable[2] : addressEnumerable[3];
            Assert.IsTrue(unHealthyUriFromEnumerable.IsUnhealthy());

            if (!addressEnumerable[2].IsUnhealthy() && !addressEnumerable[3].IsUnhealthy())
            {
                Assert.Fail("Unhealthy replicas should be at the end of the list.");
            }

            await Task.Delay(TimeSpan.FromSeconds(65));

            addressEnumerable = new AddressEnumerator().GetTransportAddresses(uris, failedReplicas, false).ToList();
            transportAddressUrisOutput = new HashSet<TransportAddressUri>(addressEnumerable);

            // Test to see all failed address are at the end
            foreach (TransportAddressUri uri in addressEnumerable)
            {
                transportAddressUrisOutput.Add(uri);
                Assert.IsFalse(uri.IsUnhealthy(), $"{DateTime.UtcNow}, unhealthy: {timeSetToUnhealthy}, uri:{uri} {uri.IsUnhealthy()}");
            }
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task TransportAddressUriUnHealthyTimeoutTest()
        {
            TransportAddressUri uri = new TransportAddressUri(new Uri("https://localhost:"));
            Assert.IsFalse(uri.IsUnhealthy(), $"New URI should be healthy. {uri.IsUnhealthy()}");
            uri.SetUnhealthy();
            DateTime timeSetUnhealthy = DateTime.UtcNow;
            Assert.IsTrue(uri.IsUnhealthy(), $"uri.SetUnhealthy called: {uri.IsUnhealthy()}. Should be unhealthy for 1 minute");

            // Timeout is 1 minute. Give 5 second buffer to avoid race condition
            await Task.Delay(TimeSpan.FromSeconds(62));

            Assert.IsFalse(uri.IsUnhealthy(), $"Is unhealthy after 1 minute. Set time: {timeSetUnhealthy}; current:{DateTime.UtcNow}");
            uri.SetUnhealthy();
            Assert.IsTrue(uri.IsUnhealthy(), $"uri.SetUnhealthy called after it timedout: {uri.IsUnhealthy()}. Should be unhealthy for 1 minute");
        }

        /// <summary>
        /// Test to validate that when replica validation is enabled, the AddressEnumerator de-prioritizes the failed
        /// replicas and re-order the replica set in the order of Connected/ Unknown >> UnhealthyPending >> Unhealthy.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(4, 2, 3, 0, 1)]
        [DataRow(4, 1, 0, 2, 3)]
        [DataRow(4, 3, 1, 2, 0)]
        public void GetTransportAddresses_WithReplicaValidationEnabled_ShouldReorderAndDeprioritizeFailedReplicas(
            int totalUriCount,
            int healthyIndex,
            int unknownIndex,
            int unhealthyPendingIndex,
            int unhealthyIndex)
        {
            // Arrange.
            List<TransportAddressUri> transportAddresses = new(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                transportAddresses.Add(
                    new TransportAddressUri(
                        addressUri: new Uri(
                            uriString: $"rntbd://test.azure.com:15211/partitions/{i}s")));
            }

            transportAddresses[unhealthyPendingIndex].SetUnhealthy();
            transportAddresses[unhealthyIndex].SetUnhealthy();
            transportAddresses[unhealthyPendingIndex].SetRefreshedIfUnhealthy();
            transportAddresses[healthyIndex].SetConnected();

            DocumentServiceRequestContext requestContext = new();
            requestContext.FailedEndpoints.Value.TryAdd(transportAddresses[unhealthyIndex], true);

            // Act.
            IEnumerable<TransportAddressUri> addressEnumerable = new AddressEnumerator()
                .GetTransportAddresses(
                    transportAddressUris: transportAddresses,
                    failedEndpoints: requestContext.FailedEndpoints,
                    replicaAddressValidationEnabled: true);

            // Assert.
            Assert.IsNotNull(addressEnumerable);
            Assert.AreEqual(
                expected: totalUriCount,
                actual: addressEnumerable.Count());

            int idx = 0;
            foreach (TransportAddressUri transportAddressUri in addressEnumerable)
            {
                if (idx == 0)
                {
                    Assert.IsTrue(
                        condition: transportAddresses[healthyIndex].ToString().Equals(transportAddressUri.ToString(), StringComparison.Ordinal) || transportAddresses[unknownIndex].ToString().Equals(transportAddressUri.ToString(), StringComparison.Ordinal),
                        message: "Validate that the healthy replicas are always at the begining along with unknown replicas. Both unknown and healthy replicas have the same probability of getting selected.");
                }
                if (idx == 1)
                {
                    Assert.IsTrue(
                        condition: transportAddresses[healthyIndex].ToString().Equals(transportAddressUri.ToString(), StringComparison.Ordinal) || transportAddresses[unknownIndex].ToString().Equals(transportAddressUri.ToString(), StringComparison.Ordinal),
                        message: "Validate that the unknown replicas are always at the begining along with healthy replicas. Both unknown and healthy replicas have the same probability of getting selected.");
                }
                if (idx == 2)
                {
                    Assert.AreEqual(
                        expected: transportAddresses[unhealthyPendingIndex].ToString(),
                        actual: transportAddressUri.ToString(),
                        message: "Validate that the unhealthy-pending replicas are always followed by the unknown or healthy replica set.");
                }
                if (idx == 3)
                {
                    Assert.AreEqual(
                        expected: transportAddresses[unhealthyIndex].ToString(),
                        actual: transportAddressUri.ToString(),
                        message: "Validate that the unhealthy replicas are always towards the end.");
                }
                idx++;
            }
        }

        /// <summary>
        /// Test to validate that when replica validation is enabled, and the replicas with unknown and
        /// unhealthy pending statuses remains idle for more than one minute, they eventually gets transitioned
        /// to healthy category.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void GetTransportAddresses_WithReplicaValidationEnabledAndUriRemainUnknownForOneMinite_ShouldReorderAndDeprioritizeFailedReplicas()
        {
            // Arrange.
            int totalUriCount = 4, healthyIndex = 0, unknownIndex = 1, unhealthyIndex = 2, unhealthyPendingIndex = 3;
            List<TransportAddressUri> transportAddresses = new(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                transportAddresses.Add(
                    new TransportAddressUri(
                        addressUri: new Uri(
                            uriString: $"rntbd://test.azure.com:15211/partitions/{i}s")));
            }

            transportAddresses[unhealthyIndex].SetUnhealthy();
            transportAddresses[unhealthyPendingIndex].SetUnhealthy();
            transportAddresses[unhealthyPendingIndex].SetRefreshedIfUnhealthy();
            transportAddresses[healthyIndex].SetConnected();

            DocumentServiceRequestContext requestContext = new();
            requestContext.FailedEndpoints.Value.TryAdd(transportAddresses[unhealthyIndex], true);

            // Act.
            IEnumerable<TransportAddressUri> addressEnumerable = new AddressEnumerator()
                .GetTransportAddresses(
                    transportAddressUris: transportAddresses,
                    failedEndpoints: requestContext.FailedEndpoints,
                    replicaAddressValidationEnabled: true);

            // Assert.
            Assert.IsNotNull(addressEnumerable);
            Assert.AreEqual(totalUriCount, addressEnumerable.Count());
            Assert.AreEqual(1, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.Connected)).Count(), "Validate that the healthy replica is present in the enumerator.");
            Assert.AreEqual(1, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.Unknown)).Count(), "Validate that the unknown replica is present in the enumerator.");
            Assert.AreEqual(1, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.UnhealthyPending)).Count(), "Validate that the unhealthy pending replica is present in the enumerator.");
            Assert.AreEqual(1, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.Unhealthy)).Count(), "Validate that the unhealthy replica is present in the enumerator.");

            // Arrange: Adds delay for two minutes to transition the unknown and unhealthy pending replicas intoto healthy category.
            TestUtils.AddMinuteToDateTimeFieldUsingReflection(
                            objectName: transportAddresses[unknownIndex].GetCurrentHealthState(),
                            fieldName: "lastUnknownTimestamp",
                            delayInMinutes: -2);

            TestUtils.AddMinuteToDateTimeFieldUsingReflection(
                            objectName: transportAddresses[unhealthyPendingIndex].GetCurrentHealthState(),
                            fieldName: "lastUnhealthyPendingTimestamp",
                            delayInMinutes: -2);

            // Act.
            addressEnumerable = new AddressEnumerator()
                .GetTransportAddresses(
                    transportAddressUris: transportAddresses,
                    failedEndpoints: requestContext.FailedEndpoints,
                    replicaAddressValidationEnabled: true);

            // Assert.
            Assert.AreEqual(3, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.Connected)).Count(), "Validate that the unknown and unhealthy replicas are transitioned to healthy category after two minute of idle time.");
            Assert.AreEqual(1, addressEnumerable.Where((x) => (x.GetEffectiveHealthStatus() == TransportAddressHealthState.HealthStatus.Unhealthy)).Count(), "Validate that the unhealthy replica is present in the enumerator.");
        }

        /// <summary>
        /// Test to validate that when replica validation is disabled, the AddressEnumerator de-prioritizes the failed
        /// replicas and re-order the replica set in the order of Connected/Unknown/UnhealthyPending >> Unhealthy. This
        /// means the selection of the replica is done with equal priority on statuses Connected, Unknown, UnhealthyPending
        /// over Unhealthy.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(4, 0, 3)]
        [DataRow(4, 2, 0)]
        [DataRow(4, 1)]
        public void GetTransportAddresses_WithReplicaValidationDisabled_ShouldDeprioritizeOnlyFailedReplicas(
            int totalUriCount,
            params int[] unhealthyIndexes)
        {
            // Arrange.
            List<TransportAddressUri> transportAddresses = new(totalUriCount);
            HashSet<TransportAddressUri> unhealthyAddresses = new(), otherAddresses = new();
            DocumentServiceRequestContext requestContext = new();
            for (int i = 0; i < totalUriCount; i++)
            {
                transportAddresses.Add(
                    new TransportAddressUri(
                        addressUri: new Uri(
                            uriString: $"rntbd://test.azure.com:15211/partitions/{i}s")));

                if (unhealthyIndexes.Contains(i))
                {
                    transportAddresses[i].SetUnhealthy();
                    unhealthyAddresses.Add(transportAddresses[i]);
                    requestContext.FailedEndpoints.Value.TryAdd(transportAddresses[i], true);
                }
                else
                {
                    if (i % 2 == 0)
                    {
                        transportAddresses[i].SetConnected();
                    }
                    otherAddresses.Add(transportAddresses[i]);
                }
            }

            // Act.
            IEnumerable<TransportAddressUri> addressEnumerable = new AddressEnumerator()
                .GetTransportAddresses(
                    transportAddressUris: transportAddresses,
                    failedEndpoints: requestContext.FailedEndpoints,
                    replicaAddressValidationEnabled: false);

            // Assert.
            Assert.IsNotNull(addressEnumerable);
            Assert.AreEqual(totalUriCount, addressEnumerable.Count());

            List<TransportAddressUri> returnedAddressList = addressEnumerable.ToList();
            int lastIndex = returnedAddressList.Count - 1, failedAddressesEncountered = 0;
            while (lastIndex >= 0)
            {
                if (failedAddressesEncountered < unhealthyAddresses.Count)
                {
                    Assert.IsTrue(
                        condition: unhealthyAddresses.Contains(returnedAddressList[lastIndex]),
                        message: "Validate that the unhealthy replicas are always towards the end.");

                    failedAddressesEncountered++;
                }
                else
                {
                    Assert.IsTrue(
                        condition: otherAddresses.Contains(returnedAddressList[lastIndex]),
                        message: "Validate that the healthy/unknown/unhealthy-pending replicas are always at the begining.");
                }
                lastIndex--;
            }
        }

        /// <summary>
        /// Test to validate that when replica validation is disabled, the AddressEnumerator de-prioritizes the failed
        /// replicas and re-order the replica set in the order of Connected/Unknown/UnhealthyPending >> Unhealthy. This
        /// means the selection of the replica is done with equal priority on statuses Connected, Unknown, UnhealthyPending
        /// over Unhealthy.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void ReorderAddressesWhenReplicaValidationEnabled_WithMultipleReplicasWithSameStatus_ShouldMaintainTheOriginalOrderingOfTheReplicas()
        {
            // Arrange.
            int totalUriCount = 4;
            List<TransportAddressUri> transportAddresses = new(totalUriCount);
            for (int i = 0; i < totalUriCount; i++)
            {
                transportAddresses.Add(
                    new TransportAddressUri(
                        addressUri: new Uri(
                            uriString: $"rntbd://test.azure.com:15211/partitions/{i}s")));

            }

            // Act.
            AddressEnumerator addressEnumerator = new();
            MethodInfo reorderAddressesWhenReplicaValidationEnabledMethod = addressEnumerator
                .GetType()
                .GetMethod("ReorderAddressesWhenReplicaValidationEnabled", BindingFlags.NonPublic | BindingFlags.Static);

            IEnumerable<TransportAddressUri> sortedTransportAddressEnumerator = (IEnumerable<TransportAddressUri>)reorderAddressesWhenReplicaValidationEnabledMethod
                .Invoke(
                obj: addressEnumerator,
                parameters: new object[] { transportAddresses, new ConcurrentDictionary<TransportAddressUri, bool>() });

            // Assert.
            List<TransportAddressUri> sortedTransportAddressList = sortedTransportAddressEnumerator.ToList();
            Assert.IsNotNull(sortedTransportAddressList);
            for (int i = 0; i < totalUriCount; i++)
            {
                Assert.AreEqual(transportAddresses[i], sortedTransportAddressList[i]);

            }
        }

        private static IEnumerable<IEnumerable<T>> GetPermutations<T>(
            IEnumerable<T> set,
            IEnumerable<T> subset = null)
        {
            subset ??= new T[] { };

            if (!set.Any())
            {
                yield return subset;
            }

            for (int i = 0; i < set.Count(); i++)
            {
                IEnumerable<T> newSet = set.Take(i).Concat(set.Skip(i + 1));
                IEnumerable<T> newSubSet = subset.Concat(set.Skip(i).Take(1));
                foreach (IEnumerable<T> permutation in GetPermutations(newSet, newSubSet))
                {
                    yield return permutation;
                }
            }
        }
    }
}