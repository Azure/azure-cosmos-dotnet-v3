// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;

    internal static class DistinctHash
    {
        private static readonly UInt128 RootHashSeed = UInt128.Create(0xbfc2359eafc0e2b7, 0x8846e00284c4cf1f);

        public static UInt128 GetHash(CosmosElement cosmosElement)
        {
            return DistinctHash.GetHash(cosmosElement, DistinctHash.RootHashSeed);
        }

        private static UInt128 GetHash(CosmosElement cosmosElement, UInt128 seed)
        {
            if (cosmosElement == null)
            {
                return DistinctHash.GetUndefinedHash(seed);
            }

            return cosmosElement.Accept(CosmosElementHasher.Singleton, seed);
        }

        private static UInt128 GetUndefinedHash(UInt128 seed)
        {
            return seed;
        }

        private sealed class CosmosElementHasher : ICosmosElementVisitor<UInt128, UInt128>
        {
            public static readonly CosmosElementHasher Singleton = new CosmosElementHasher();

            private static readonly UInt128 NullHashSeed = UInt128.Create(0x1380f68bb3b0cfe4, 0x156c918bf564ee48);

            private static readonly UInt128 FalseHashSeed = UInt128.Create(0xc1be517fe893b40c, 0xe9fc8a4c531cd0dd);

            private static readonly UInt128 TrueHashSeed = UInt128.Create(0xf86d4abf9a412e74, 0x788488365c8a985d);

            private static readonly UInt128 NumberHashSeed = UInt128.Create(0x2400e8b894ce9c2a, 0x790be1eabd7b9481);

            private static readonly UInt128 StringHashSeed = UInt128.Create(0x61f53f0a44204cfb, 0x09481be8ef4b56dd);

            private static readonly UInt128 ArrayHashSeed = UInt128.Create(0xfa573b014c4dc18e, 0xa014512c858eb115);

            private static readonly UInt128 ObjectHashSeed = UInt128.Create(0x77b285ac511aef30, 0x3dcf187245822449);

            private static readonly UInt128 ArrayIndexHashSeed = UInt128.Create(0xfe057204216db999, 0x5b1cc3178bd9c593);

            private static readonly UInt128 PropertyNameHashSeed = UInt128.Create(0xc915dde058492a8a, 0x7c8be2eba72e4634);

            private static readonly UInt128 BinaryHashSeed = UInt128.Create(0x54841d59fe1ea46c, 0xd4edb0ba5c59766b);

            private static readonly UInt128 GuidHashSeed = UInt128.Create(0x53b5b8939b790f4b, 0x7cc5e09441fd6cb1);

            private CosmosElementHasher()
            {
                // Private constructor, since this is a singleton class.
            }

            public UInt128 Visit(CosmosArray cosmosArray, UInt128 seed)
            {
                // Start the array with a distinct hash, so that empty array doesn't hash to another value.
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.ArrayHashSeed, seed);

                // Incorporate all the array items into the hash.
                for (int index = 0; index < cosmosArray.Count; index++)
                {
                    CosmosElement arrayItem = cosmosArray[index];

                    // Order of array items matter in equality check, so we add the index just to be safe.
                    // For now we know that murmurhash will correctly give a different hash for 
                    // [true, false, true] and [true, true, false]
                    // due to the way the seed works.
                    // But we add the index just incase that property does not hold in the future.
                    UInt128 arrayItemSeed = CosmosElementHasher.ArrayIndexHashSeed + index;
                    hash = arrayItem.Accept(this, arrayItemSeed);
                }

                return hash;
            }

            public UInt128 Visit(CosmosBinary cosmosBinary, UInt128 seed)
            {
                // Hash with binary seed to differntiate between empty binary and no binary.
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.BinaryHashSeed, seed);

                // TODO: replace this with Span based hashing.
                hash = CosmosElementHasher.GetHash(cosmosBinary.Value.ToArray(), seed);
                return hash;
            }

            public UInt128 Visit(CosmosBoolean cosmosBoolean, UInt128 seed)
            {
                return CosmosElementHasher.GetHash(
                    cosmosBoolean.Value ? CosmosElementHasher.TrueHashSeed : CosmosElementHasher.FalseHashSeed,
                    seed);
            }

            public UInt128 Visit(CosmosGuid cosmosGuid, UInt128 seed)
            {
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.GuidHashSeed, seed);
                hash = CosmosElementHasher.GetHash(cosmosGuid.Value.ToByteArray(), seed);
                return hash;
            }

            public UInt128 Visit(CosmosNull cosmosNull, UInt128 seed)
            {
                return CosmosElementHasher.GetHash(CosmosElementHasher.NullHashSeed, seed);
            }

            public UInt128 Visit(CosmosNumber cosmosNumber, UInt128 seed)
            {
                // TODO: hash differently based on the type.
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.NumberHashSeed, seed);
                double number;
                if (cosmosNumber.IsFloatingPoint)
                {
                    number = cosmosNumber.AsFloatingPoint().Value;
                }
                else
                {
                    number = cosmosNumber.AsInteger().Value;
                }

                hash = CosmosElementHasher.GetHash((UInt128)BitConverter.DoubleToInt64Bits(number), hash);
                return hash;
            }

            public UInt128 Visit(CosmosObject cosmosObject, UInt128 seed)
            {
                // Start the object with a distinct hash, so that empty object doesn't hash to another value.
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.ObjectHashSeed, seed);

                //// Intermediate hashes of all the properties, which we don't want to xor with the final hash
                //// otherwise the following will collide:
                ////{
                ////    "pet":{
                ////        "name":"alice",
                ////        "age":5
                ////    },
                ////    "pet2":{
                ////        "name":"alice",
                ////        "age":5
                ////    }
                ////}
                ////
                ////{
                ////    "pet":{
                ////        "name":"bob",
                ////        "age":5
                ////    },
                ////    "pet2":{
                ////        "name":"bob",
                ////        "age":5
                ////    }
                ////}
                //// because they only differ on the name, but it gets repeated meaning that 
                //// hash({"name":"bob", "age":5}) ^ hash({"name":"bob", "age":5}) is the same as
                //// hash({"name":"alice", "age":5}) ^ hash({"name":"alice", "age":5})
                UInt128 intermediateHash = 0;

                // Property order should not result in a different hash.
                // This is consistent with equality comparison.
                foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
                {
                    UInt128 nameHash = CosmosString.Create(kvp.Key).Accept(this, CosmosElementHasher.PropertyNameHashSeed);
                    UInt128 propertyHash = kvp.Value.Accept(this, nameHash);

                    //// xor is symmetric meaning that a ^ b = b ^ a
                    //// Which is great since now we can add the property hashes to the intermediate hash
                    //// in any order and get the same result, which upholds our definition of equality.
                    //// Note that we don't have to worry about a ^ a = 0 = b ^ b for duplicate property values,
                    //// since the hash of property values are seeded with the hash of property names,
                    //// which are unique within an object.
                    intermediateHash ^= propertyHash;
                }

                // Only if the object was not empty do we want to bring in the intermediate hash.
                if (intermediateHash > 0)
                {
                    hash = CosmosElementHasher.GetHash(intermediateHash, hash);
                }

                return hash;
            }

            public UInt128 Visit(CosmosString cosmosString, UInt128 seed)
            {
                // TODO: replace this with span based hashing and try get buffered string value.
                UInt128 hash = CosmosElementHasher.GetHash(CosmosElementHasher.StringHashSeed, seed);
                byte[] stringBytes = Encoding.UTF8.GetBytes(cosmosString.Value);
                return CosmosElementHasher.GetHash(stringBytes, hash);
            }

            private static UInt128 GetHash(UInt128 value, UInt128 seed)
            {
                return CosmosElementHasher.GetHash(UInt128.ToByteArray(value), seed);
            }

            private static UInt128 GetHash(byte[] bytes, UInt128 seed)
            {
                // TODO: Have MurmurHash3 work on Span<T> instead.
                Microsoft.Azure.Documents.UInt128 hash128 = Microsoft.Azure.Documents.Routing.MurmurHash3.Hash128(
                    bytes,
                    bytes.Length,
                    Microsoft.Azure.Documents.UInt128.Create(seed.GetLow(), seed.GetHigh()));
                return UInt128.Create(hash128.GetLow(), hash128.GetHigh());
            }
        }
    }
}
