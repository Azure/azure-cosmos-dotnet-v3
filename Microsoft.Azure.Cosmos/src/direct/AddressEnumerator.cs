//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
        /// This uses the Fisher–Yates shuffle algorithm to return a random order
        /// of Transport Address URIs
        /// </summary>
        public IEnumerable<TransportAddressUri> GetTransportAddresses(IReadOnlyList<TransportAddressUri> transportAddressUris)
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
    }
}