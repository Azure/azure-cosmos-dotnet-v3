// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;

    using CosmosUInt128 = Microsoft.Azure.Cosmos.UInt128;

    internal static class DistinctHash
    {
        private static readonly CosmosUInt128 RootHashSeed = CosmosUInt128.Create(0xbfc2359eafc0e2b7, 0x8846e00284c4cf1f);

        public static CosmosUInt128 GetHash(CosmosElement cosmosElement)
        {
            return GetHash(cosmosElement, RootHashSeed);
        }

        private static CosmosUInt128 GetHash(CosmosElement cosmosElement, CosmosUInt128 seed)
        {
            return cosmosElement.Accept(CosmosElementHasher.Singleton, seed);
        }

        private sealed class CosmosElementHasher : ICosmosElementVisitor<CosmosUInt128, CosmosUInt128>
        {
            public static readonly CosmosElementHasher Singleton = new CosmosElementHasher();

            private static class HashSeeds
            {
                public static readonly CosmosUInt128 Null = CosmosUInt128.Create(0x1380f68bb3b0cfe4, 0x156c918bf564ee48);
                public static readonly CosmosUInt128 False = CosmosUInt128.Create(0xc1be517fe893b40c, 0xe9fc8a4c531cd0dd);
                public static readonly CosmosUInt128 True = CosmosUInt128.Create(0xf86d4abf9a412e74, 0x788488365c8a985d);
                public static readonly CosmosUInt128 String = CosmosUInt128.Create(0x61f53f0a44204cfb, 0x09481be8ef4b56dd);
                public static readonly CosmosUInt128 Array = CosmosUInt128.Create(0xfa573b014c4dc18e, 0xa014512c858eb115);
                public static readonly CosmosUInt128 Object = CosmosUInt128.Create(0x77b285ac511aef30, 0x3dcf187245822449);
                public static readonly CosmosUInt128 ArrayIndex = CosmosUInt128.Create(0xfe057204216db999, 0x5b1cc3178bd9c593);
                public static readonly CosmosUInt128 PropertyName = CosmosUInt128.Create(0xc915dde058492a8a, 0x7c8be2eba72e4634);
                public static readonly CosmosUInt128 Binary = CosmosUInt128.Create(0x54841d59fe1ea46c, 0xd4edb0ba5c59766b);
                public static readonly CosmosUInt128 Guid = CosmosUInt128.Create(0x53b5b8939b790f4b, 0x7cc5e09441fd6cb1);
            }

            private static class RootCache
            {
                public static readonly CosmosUInt128 Null = MurmurHash3.Hash128(HashSeeds.Null, RootHashSeed);
                public static readonly CosmosUInt128 False = MurmurHash3.Hash128(HashSeeds.False, RootHashSeed);
                public static readonly CosmosUInt128 True = MurmurHash3.Hash128(HashSeeds.True, RootHashSeed);
                public static readonly CosmosUInt128 String = MurmurHash3.Hash128(HashSeeds.String, RootHashSeed);
                public static readonly CosmosUInt128 Array = MurmurHash3.Hash128(HashSeeds.Array, RootHashSeed);
                public static readonly CosmosUInt128 Object = MurmurHash3.Hash128(HashSeeds.Object, RootHashSeed);
                public static readonly CosmosUInt128 Binary = MurmurHash3.Hash128(HashSeeds.Binary, RootHashSeed);
                public static readonly CosmosUInt128 Guid = MurmurHash3.Hash128(HashSeeds.Guid, RootHashSeed);
            }

            private CosmosElementHasher()
            {
                // Private constructor, since this is a singleton class.
            }

            public CosmosUInt128 Visit(CosmosArray cosmosArray, CosmosUInt128 seed)
            {
                // Start the array with a distinct hash, so that empty array doesn't hash to another value.
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Array : MurmurHash3.Hash128(HashSeeds.Array, seed);

                // Incorporate all the array items into the hash.
                for (int index = 0; index < cosmosArray.Count; index++)
                {
                    CosmosElement arrayItem = cosmosArray[index];

                    if (arrayItem is not CosmosUndefined)
                    {
                        // Order of array items matter in equality check, so we add the index just to be safe.
                        // For now we know that murmurhash will correctly give a different hash for 
                        // [true, false, true] and [true, true, false]
                        // due to the way the seed works.
                        // But we add the index just incase that property does not hold in the future.
                        CosmosUInt128 arrayItemSeed = HashSeeds.ArrayIndex + (CosmosUInt128)index;
                        hash = MurmurHash3.Hash128(arrayItem.Accept(this, arrayItemSeed), hash);
                    }
                }

                return hash;
            }

            public CosmosUInt128 Visit(CosmosBinary cosmosBinary, CosmosUInt128 seed)
            {
                // Hash with binary seed to differntiate between empty binary and no binary.
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Binary : MurmurHash3.Hash128(HashSeeds.Binary, seed);
                hash = MurmurHash3.Hash128(cosmosBinary.Value.Span, hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosBoolean cosmosBoolean, CosmosUInt128 seed)
            {
                if (seed == RootHashSeed)
                {
                    return cosmosBoolean.Value ? RootCache.True : RootCache.False;
                }

                return MurmurHash3.Hash128(
                    cosmosBoolean.Value ? HashSeeds.True : HashSeeds.False,
                    seed);
            }

            public CosmosUInt128 Visit(CosmosGuid cosmosGuid, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Guid : MurmurHash3.Hash128(HashSeeds.Guid, seed);
                hash = MurmurHash3.Hash128(cosmosGuid.Value.ToByteArray(), hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosNull cosmosNull, CosmosUInt128 seed)
            {
                if (seed == RootHashSeed)
                {
                    return RootCache.Null;
                }

                return MurmurHash3.Hash128(HashSeeds.Null, seed);
            }

            public CosmosUInt128 Visit(CosmosUndefined cosmosUndefined, CosmosUInt128 seed)
            {
                // undefined is ignored while hashing
                return seed;
            }

            public CosmosUInt128 Visit(CosmosNumber cosmosNumber, CosmosUInt128 seed)
            {
                return cosmosNumber.Accept(CosmosNumberHasher.Singleton, seed);
            }

            public CosmosUInt128 Visit(CosmosObject cosmosObject, CosmosUInt128 seed)
            {
                // Start the object with a distinct hash, so that empty object doesn't hash to another value.
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Object : MurmurHash3.Hash128(HashSeeds.Object, seed);

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
                CosmosUInt128 intermediateHash = 0;

                // Property order should not result in a different hash.
                // This is consistent with equality comparison.
                foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
                {
                    if (kvp.Value is not CosmosUndefined)
                    {
                        CosmosUInt128 nameHash = MurmurHash3.Hash128(kvp.Key, MurmurHash3.Hash128(HashSeeds.String, HashSeeds.PropertyName));
                        CosmosUInt128 propertyHash = kvp.Value.Accept(this, nameHash);

                        //// xor is symmetric meaning that a ^ b = b ^ a
                        //// Which is great since now we can add the property hashes to the intermediate hash
                        //// in any order and get the same result, which upholds our definition of equality.
                        //// Note that we don't have to worry about a ^ a = 0 = b ^ b for duplicate property values,
                        //// since the hash of property values are seeded with the hash of property names,
                        //// which are unique within an object.
                        intermediateHash ^= propertyHash;
                    }
                }

                // Only if the object was not empty do we want to bring in the intermediate hash.
                if (intermediateHash > 0)
                {
                    hash = MurmurHash3.Hash128(intermediateHash, hash);
                }

                return hash;
            }

            public CosmosUInt128 Visit(CosmosString cosmosString, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.String : MurmurHash3.Hash128(HashSeeds.String, seed);
                UtfAnyString utfAnyString = cosmosString.Value;
                hash = utfAnyString.IsUtf8
                    ? MurmurHash3.Hash128(utfAnyString.ToUtf8String().Span.Span, hash)
                    : MurmurHash3.Hash128(utfAnyString.ToString(), hash);

                return hash;
            }
        }

        private sealed class CosmosNumberHasher : ICosmosNumberVisitor<CosmosUInt128, CosmosUInt128>
        {
            public static readonly CosmosNumberHasher Singleton = new CosmosNumberHasher();

            public static class HashSeeds
            {
                public static readonly CosmosUInt128 Number64 = CosmosUInt128.Create(0x2400e8b894ce9c2a, 0x790be1eabd7b9481);
                public static readonly CosmosUInt128 Float32 = CosmosUInt128.Create(0x881c51c28fb61016, 0x1decd039cd24bd4b);
                public static readonly CosmosUInt128 Float64 = CosmosUInt128.Create(0x62fb48cc659963a0, 0xe9e690779309c403);
                public static readonly CosmosUInt128 Int8 = CosmosUInt128.Create(0x0007978411626daa, 0x89933677a85444b7);
                public static readonly CosmosUInt128 Int16 = CosmosUInt128.Create(0xe7a19001d3211c09, 0x33e0ba9fb8bc7940);
                public static readonly CosmosUInt128 Int32 = CosmosUInt128.Create(0x0320dc908e0d3e71, 0xf575de218f09ffa5);
                public static readonly CosmosUInt128 Int64 = CosmosUInt128.Create(0xed93baf7fdc76638, 0x0d5733c37e079869);
                public static readonly CosmosUInt128 UInt32 = CosmosUInt128.Create(0x78c441a2d2e9bb6e, 0xac88cb880ccda71d);
            }

            public static class RootCache
            {
                public static readonly CosmosUInt128 Number64 = MurmurHash3.Hash128(HashSeeds.Number64, RootHashSeed);
                public static readonly CosmosUInt128 Float32 = MurmurHash3.Hash128(HashSeeds.Float32, RootHashSeed);
                public static readonly CosmosUInt128 Float64 = MurmurHash3.Hash128(HashSeeds.Float64, RootHashSeed);
                public static readonly CosmosUInt128 Int8 = MurmurHash3.Hash128(HashSeeds.Int8, RootHashSeed);
                public static readonly CosmosUInt128 Int16 = MurmurHash3.Hash128(HashSeeds.Int16, RootHashSeed);
                public static readonly CosmosUInt128 Int32 = MurmurHash3.Hash128(HashSeeds.Int32, RootHashSeed);
                public static readonly CosmosUInt128 Int64 = MurmurHash3.Hash128(HashSeeds.Int64, RootHashSeed);
                public static readonly CosmosUInt128 UInt32 = MurmurHash3.Hash128(HashSeeds.UInt32, RootHashSeed);
            }

            private CosmosNumberHasher()
            {
                // Private constructor, since this class is a singleton.
            }

            public CosmosUInt128 Visit(CosmosFloat32 cosmosFloat32, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Float32 : MurmurHash3.Hash128(HashSeeds.Float32, seed);
                float value = cosmosFloat32.GetValue();

                // Normalize 0.0f and -0.0f value
                // https://stackoverflow.com/questions/3139538/is-minus-zero-0-equivalent-to-zero-0-in-c-sharp
                if (value == 0.0f)
                {
                    value = 0;
                }

                hash = MurmurHash3.Hash128(BitConverter.DoubleToInt64Bits(value), hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosFloat64 cosmosFloat64, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Float64 : MurmurHash3.Hash128(HashSeeds.Float64, seed);
                double value = cosmosFloat64.GetValue();

                // Normalize 0.0 and -0.0 value
                // https://stackoverflow.com/questions/3139538/is-minus-zero-0-equivalent-to-zero-0-in-c-sharp
                if (value == 0.0)
                {
                    value = 0;
                }

                hash = MurmurHash3.Hash128(BitConverter.DoubleToInt64Bits(value), hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosInt16 cosmosInt16, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Int16 : MurmurHash3.Hash128(HashSeeds.Int16, seed);
                short value = cosmosInt16.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosInt32 cosmosInt32, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Int32 : MurmurHash3.Hash128(HashSeeds.Int32, seed);
                int value = cosmosInt32.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosInt64 cosmosInt64, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Int64 : MurmurHash3.Hash128(HashSeeds.Int64, seed);
                long value = cosmosInt64.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosInt8 cosmosInt8, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Int8 : MurmurHash3.Hash128(HashSeeds.Int8, seed);
                sbyte value = cosmosInt8.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public CosmosUInt128 Visit(CosmosNumber64 cosmosNumber64, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.Number64 : MurmurHash3.Hash128(HashSeeds.Number64, seed);
                Number64 value = cosmosNumber64.GetValue();
                Number64.DoubleEx doubleExValue = Number64.ToDoubleEx(value);
                return MurmurHash3.Hash128(doubleExValue, hash);
            }

            public CosmosUInt128 Visit(CosmosUInt32 cosmosUInt32, CosmosUInt128 seed)
            {
                CosmosUInt128 hash = seed == RootHashSeed ? RootCache.UInt32 : MurmurHash3.Hash128(HashSeeds.UInt32, seed);
                uint value = cosmosUInt32.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }
        }
    }
}
