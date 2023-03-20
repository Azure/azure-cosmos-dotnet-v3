//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Microsoft.Azure.Documents.HttpConstants;

    /// <summary>
    /// AddressEnumerator randomly iterates a list of TransportAddressUris.
    /// </summary>
    internal sealed class AddressEnumerator : IAddressEnumerator
    {
        [ThreadStatic]
        private static Random random;

        private static int GenerateNextRandom(int start, int maxValue)
        {
            if (AddressEnumerator.random == null)
            {
                // Generate random numbers with a seed so that not all the threads without random available
                // start producing the same sequence.
                AddressEnumerator.random = CustomTypeExtensions.GetRandomNumber();
            }

            return AddressEnumerator.random.Next(start, maxValue);
        }

        /// <summary>
        /// This return a random order of the addresses if there are no addresses that failed. Else it moves the failes addresses to the end.
        /// </summary>
        public IEnumerable<TransportAddressUri> GetTransportAddresses(IReadOnlyList<TransportAddressUri> transportAddressUris,
                                                                      Lazy<HashSet<TransportAddressUri>> failedEndpoints,
                                                                      bool replicaAddressValidationEnabled)
        {
            if (failedEndpoints == null)
            {
                throw new ArgumentNullException(nameof(failedEndpoints));
            }

            // Get a random Permutation.
            IEnumerable<TransportAddressUri> randomPermutation = this.GetTransportAddresses(transportAddressUris);

            // We reorder the replica set with the following rule, to avoid landing on to any unhealthy replica/s:
            // Scenario 1: When replica validation is enabled, the replicas will be reordered by the preference of
            // Connected/Unknown > UnhealthyPending > Unhealthy.
            // Scenario 2: When replica validation is disabled, the replicas will be reordered by the preference of
            // Connected/Unknown/UnhealthyPending > Unhealthy.
            return AddressEnumerator.ReorderReplicasByHealthStatus(
                randomPermutation: randomPermutation,
                lazyFailedReplicasPerRequest: failedEndpoints,
                replicaAddressValidationEnabled: replicaAddressValidationEnabled);
        }

        /// <summary>
        /// This uses the Fisher–Yates shuffle algorithm to return a random order
        /// of Transport Address URIs
        /// </summary>
        private IEnumerable<TransportAddressUri> GetTransportAddresses(IReadOnlyList<TransportAddressUri> transportAddressUris)
        {
            if (transportAddressUris == null)
            {
                throw new ArgumentNullException(nameof(transportAddressUris));
            }

            if (transportAddressUris.Count == 0)
            {
                return Enumerable.Empty<TransportAddressUri>();
            }

            // Permutation is faster and has less over head compared to Fisher-Yates shuffle
            // Permutation is optimized for most common scenario where replica count is 5 or less
            // Fisher-Yates shuffle is used in-case the passed in URI list is larger than the predefined permutation list.
            if (AddressEnumeratorUsingPermutations.IsSizeInPermutationLimits(transportAddressUris.Count))
            {
                return AddressEnumeratorUsingPermutations.GetTransportAddressUrisWithPredefinedPermutation(transportAddressUris);
            }

            return AddressEnumeratorFisherYateShuffle.GetTransportAddressUrisWithFisherYateShuffle(transportAddressUris);
        }

        /// <summary>
        /// Fisher-Yates Shuffle gives a random order of TransportAddressUri 
        /// </summary>
        private readonly struct AddressEnumeratorFisherYateShuffle
        {
            public static IEnumerable<TransportAddressUri> GetTransportAddressUrisWithFisherYateShuffle(IReadOnlyList<TransportAddressUri> transportAddressUris)
            {
                // Fisher Yates Shuffle algorithm: https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
                List<TransportAddressUri> transportAddressesCopy = transportAddressUris.ToList();

                for (int i = 0; i < transportAddressUris.Count - 1; i++)
                {
                    int randomIndex = AddressEnumerator.GenerateNextRandom(i, transportAddressUris.Count);

                    AddressEnumeratorFisherYateShuffle.Swap(transportAddressesCopy, i, randomIndex);
                    yield return transportAddressesCopy[i];
                }

                yield return transportAddressesCopy.Last();
            }

            private static void Swap(
                List<TransportAddressUri> transportAddressUris,
                int firstIndex,
                int secondIndex)
            {
                if (firstIndex == secondIndex)
                {
                    return;
                }

                TransportAddressUri temp = transportAddressUris[firstIndex];
                transportAddressUris[firstIndex] = transportAddressUris[secondIndex];
                transportAddressUris[secondIndex] = temp;
            }
        }

        /// <summary>
        /// This uses a predefined permutation list to decide the random order of TransportAddressUri
        /// This is optimized for the most common scenario. 
        /// This only requires a single random number and no additional allocation to track items already returned. 
        /// </summary>
        private readonly struct AddressEnumeratorUsingPermutations
        {
            /// <summary>
            /// Permutation by the size -> All permutation for that size -> The individual permutation
            /// </summary>
            private static readonly IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> AllPermutationsOfIndexesBySize;

            static AddressEnumeratorUsingPermutations()
            {
                List<IReadOnlyList<IReadOnlyList<int>>> allPermutationBySize = new List<IReadOnlyList<IReadOnlyList<int>>>();
                // Cosmos DB normally has 4 replicas, but for failover scenario it could be more.
                // Having permutations up to 6 will cover all the common scenarios.
                for (int i = 0; i <= 6; i++)
                {
                    List<IReadOnlyList<int>> permutations = new List<IReadOnlyList<int>>();
                    AddressEnumeratorUsingPermutations.PermuteIndexPositions(Enumerable.Range(0, i).ToArray(), 0, i, permutations);
                    allPermutationBySize.Add(permutations);
                }

                AddressEnumeratorUsingPermutations.AllPermutationsOfIndexesBySize = allPermutationBySize;
            }

            public static bool IsSizeInPermutationLimits(int size)
            {
                return size < AllPermutationsOfIndexesBySize.Count;
            }

            public static IEnumerable<TransportAddressUri> GetTransportAddressUrisWithPredefinedPermutation(IReadOnlyList<TransportAddressUri> transportAddressUris)
            {
                IReadOnlyList<IReadOnlyList<int>> allPermutationsForSpecificSize = AddressEnumeratorUsingPermutations.AllPermutationsOfIndexesBySize[transportAddressUris.Count];
                int permutation = AddressEnumerator.GenerateNextRandom(0, allPermutationsForSpecificSize.Count);

                foreach (int index in allPermutationsForSpecificSize[permutation])
                {
                    yield return transportAddressUris[index];
                }
            }

            private static void PermuteIndexPositions(
                int[] array,
                int start,
                int length,
                List<IReadOnlyList<int>> output)
            {
                if (start == length)
                {
                    output.Add(array.ToList());
                }
                else
                {
                    for (int j = start; j < length; j++)
                    {
                        AddressEnumeratorUsingPermutations.Swap(ref array[start], ref array[j]);
                        AddressEnumeratorUsingPermutations.PermuteIndexPositions(array, start + 1, length, output);
                        AddressEnumeratorUsingPermutations.Swap(ref array[start], ref array[j]); //backtrack
                    }
                }
            }

            private static void Swap(
                ref int a,
                ref int b)
            {
                int tmp = a;
                a = b;
                b = tmp;
            }
        }

        /// <summary>
        /// Reorders the replica set to prefer the healthy replicas over the unhealthy ones. When replica address validation
        /// is enabled, the address enumerator will pick the replicas in the order of their health statuses: Connected/
        /// Unknown > UnhealthyPending > Unhealthy. When replica address validation is disabled, the address enumerator will
        /// transition away Unknown/UnhealthyPending replicas and pick the replicas by preferring any of the  Connected/ Unknown
        /// / UnhealthyPending over the Unhealthy ones.
        /// </summary>
        /// <param name="randomPermutation">A list containing the permutation of the replicas.</param>
        /// <param name="lazyFailedReplicasPerRequest">A lazy set containing the unhealthy replicas.</param>
        /// <param name="replicaAddressValidationEnabled">A boolean flag indicating if the replica validation is enabled.</param>
        /// <returns>A list of <see cref="TransportAddressUri"/> ordered by the replica health statuses.</returns>
        private static IEnumerable<TransportAddressUri> ReorderReplicasByHealthStatus(
            IEnumerable<TransportAddressUri> randomPermutation,
            Lazy<HashSet<TransportAddressUri>> lazyFailedReplicasPerRequest,
            bool replicaAddressValidationEnabled)
        {
            HashSet<TransportAddressUri> failedReplicasPerRequest = null;
            if (lazyFailedReplicasPerRequest != null &&
                lazyFailedReplicasPerRequest.IsValueCreated &&
                lazyFailedReplicasPerRequest.Value.Count > 0)
            {
                failedReplicasPerRequest = lazyFailedReplicasPerRequest.Value;
            }

            if (!replicaAddressValidationEnabled)
            {
                return AddressEnumerator.MoveFailedReplicasToTheEnd(
                    addresses: randomPermutation,
                    failedReplicasPerRequest: failedReplicasPerRequest);
            }
            else
            {
                return AddressEnumerator.ReorderAddressesWhenReplicaValidationEnabled(
                    addresses: randomPermutation,
                    failedReplicasPerRequest: failedReplicasPerRequest);
            }
        }

        /// <summary>
        /// When replica address validation is enabled, the address enumerator will pick the replicas in the order of their health statuses:
        /// Connected/Unknown > UnhealthyPending > Unhealthy. The open connection handler will be used to transit away unknown/unhealthy pending status.
        /// But in case open connection request can not happen/ finish due to any reason, then after some extended time (for example 1 minute),
        /// the address enumerator will mark Unknown/ Unhealthy pending into Healthy category (please check details of TransportAddressUri.GetEffectiveHealthStatus())
        /// </summary>
        /// <param name="addresses">A random list containing all of the replica <see cref="TransportAddressUri"/> addresses.</param>
        /// <param name="failedReplicasPerRequest">A hash set containing the failed replica addresses.</param>
        /// <returns>The reordered list of <see cref="TransportAddressUri"/>.</returns>
        private static IEnumerable<TransportAddressUri> ReorderAddressesWhenReplicaValidationEnabled(
            IEnumerable<TransportAddressUri> addresses,
            HashSet<TransportAddressUri> failedReplicasPerRequest)
        {
            List<TransportAddressUri> unknownReplicas = null, failedReplicas = null, pendingReplicas = null;
            foreach (TransportAddressUri transportAddressUri in addresses)
            {
                TransportAddressHealthState.HealthStatus status = AddressEnumerator.GetEffectiveStatus(
                    addressUri: transportAddressUri,
                    failedEndpoints: failedReplicasPerRequest);

                if (status == TransportAddressHealthState.HealthStatus.Connected)
                {
                    yield return transportAddressUri;
                }
                else if (status == TransportAddressHealthState.HealthStatus.Unknown)
                {
                    unknownReplicas ??= new ();
                    unknownReplicas.Add(transportAddressUri);
                }
                else if (status == TransportAddressHealthState.HealthStatus.UnhealthyPending)
                {
                    pendingReplicas ??= new ();
                    pendingReplicas.Add(transportAddressUri);
                }
                else
                {
                    failedReplicas ??= new ();
                    failedReplicas.Add(transportAddressUri);
                }
            }

            if (unknownReplicas != null)
            {
                foreach (TransportAddressUri transportAddressUri in unknownReplicas)
                {
                    yield return transportAddressUri;
                }
            }

            if (pendingReplicas != null)
            {
                foreach (TransportAddressUri transportAddressUri in pendingReplicas)
                {
                    yield return transportAddressUri;
                }
            }

            if (failedReplicas != null)
            {
                foreach (TransportAddressUri transportAddressUri in failedReplicas)
                {
                    yield return transportAddressUri;
                }
            }
        }

        /// <summary>
        /// When replica address validation is disabled, the address enumerator will transition away Unknown/UnhealthyPending
        /// replicas and pick the replicas by preferring any of the Connected/Unknown/UnhealthyPending over Unhealthy. Therefore
        /// it moves the unhealthy replicas to the end of the replica list.
        /// </summary>
        /// <param name="addresses">A random list containing all of the replica <see cref="TransportAddressUri"/> addresses.</param>
        /// <param name="failedReplicasPerRequest">A hash set containing the failed replica addresses.</param>
        /// <returns>The reordered list of <see cref="TransportAddressUri"/>.</returns>
        private static IEnumerable<TransportAddressUri> MoveFailedReplicasToTheEnd(
            IEnumerable<TransportAddressUri> addresses,
            HashSet<TransportAddressUri> failedReplicasPerRequest)
        {
            List<TransportAddressUri> failedReplicas = null;
            foreach (TransportAddressUri transportAddressUri in addresses)
            {
                TransportAddressHealthState.HealthStatus status = AddressEnumerator.GetEffectiveStatus(
                    addressUri: transportAddressUri,
                    failedEndpoints: failedReplicasPerRequest);

                if (status == TransportAddressHealthState.HealthStatus.Connected ||
                    status == TransportAddressHealthState.HealthStatus.Unknown ||
                    status == TransportAddressHealthState.HealthStatus.UnhealthyPending)
                {
                    yield return transportAddressUri;
                }
                else
                {
                    failedReplicas ??= new ();
                    failedReplicas.Add(transportAddressUri);
                }
            }

            if (failedReplicas != null)
            {
                foreach (TransportAddressUri transportAddressUri in failedReplicas)
                {
                    yield return transportAddressUri;
                }
            }
        }

        /// <summary>
        /// Gets the effective health status of the transport address uri.
        /// </summary>
        /// <param name="addressUri">An instance of the <see cref="TransportAddressUri"/> containing the replica address.</param>
        /// <param name="failedEndpoints">A set containing the failed endpoints.</param>
        /// <returns>An instance of <see cref="TransportAddressUri.HealthStatus"/> indicating the effective health status of the address.</returns>
        private static TransportAddressHealthState.HealthStatus GetEffectiveStatus(
            TransportAddressUri addressUri,
            HashSet<TransportAddressUri> failedEndpoints)
        {
            if (failedEndpoints != null && failedEndpoints.Contains(addressUri))
            {
                return TransportAddressHealthState.HealthStatus.Unhealthy;
            }

            return addressUri.GetEffectiveHealthStatus();
        }
    }
}