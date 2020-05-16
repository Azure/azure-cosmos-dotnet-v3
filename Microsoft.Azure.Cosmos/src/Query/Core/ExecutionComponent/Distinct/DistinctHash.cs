// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
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
                UInt128 hash = MurmurHash3.Hash128(CosmosElementHasher.ArrayHashSeed, seed);

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
                    hash = MurmurHash3.Hash128(hash, arrayItem.Accept(this, arrayItemSeed));
                }

                return hash;
            }

            public UInt128 Visit(CosmosBinary cosmosBinary, UInt128 seed)
            {
                // Hash with binary seed to differntiate between empty binary and no binary.
                UInt128 hash = MurmurHash3.Hash128(CosmosElementHasher.BinaryHashSeed, seed);

                // TODO: replace this with Span based hashing.
                hash = MurmurHash3.Hash128(cosmosBinary.Value.Span, seed);
                return hash;
            }

            public UInt128 Visit(CosmosBoolean cosmosBoolean, UInt128 seed)
            {
                return MurmurHash3.Hash128(
                    cosmosBoolean.Value ? CosmosElementHasher.TrueHashSeed : CosmosElementHasher.FalseHashSeed,
                    seed);
            }

            public UInt128 Visit(CosmosGuid cosmosGuid, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosElementHasher.GuidHashSeed, seed);
                hash = MurmurHash3.Hash128(cosmosGuid.Value.ToByteArray(), seed);
                return hash;
            }

            public UInt128 Visit(CosmosNull cosmosNull, UInt128 seed)
            {
                return MurmurHash3.Hash128(CosmosElementHasher.NullHashSeed, seed);
            }

            public UInt128 Visit(CosmosNumber cosmosNumber, UInt128 seed)
            {
                return cosmosNumber.Accept(CosmosNumberHasher.Singleton, seed);
            }

            public UInt128 Visit(CosmosObject cosmosObject, UInt128 seed)
            {
                // Start the object with a distinct hash, so that empty object doesn't hash to another value.
                UInt128 hash = MurmurHash3.Hash128(CosmosElementHasher.ObjectHashSeed, seed);

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
                    hash = MurmurHash3.Hash128(intermediateHash, hash);
                }

                return hash;
            }

            public UInt128 Visit(CosmosString cosmosString, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosElementHasher.StringHashSeed, seed);
                if (cosmosString.TryGetBufferedValue(out Utf8Memory bufferedUtf8Value))
                {
                    hash = MurmurHash3.Hash128(bufferedUtf8Value.Span.Span, hash);
                }
                else
                {
                    hash = MurmurHash3.Hash128(cosmosString.Value, hash);
                }

                return hash;
            }
        }

        private sealed class CosmosNumberHasher : ICosmosNumberVisitor<UInt128, UInt128>
        {
            public static readonly CosmosNumberHasher Singleton = new CosmosNumberHasher();

            private static readonly UInt128 Number64HashSeed = UInt128.Create(0x2400e8b894ce9c2a, 0x790be1eabd7b9481);
            private static readonly UInt128 Float32HashSeed = UInt128.Create(0x881c51c28fb61016, 0x1decd039cd24bd4b);
            private static readonly UInt128 Float64HashSeed = UInt128.Create(0x62fb48cc659963a0, 0xe9e690779309c403);
            private static readonly UInt128 Int8HashSeed = UInt128.Create(0x0007978411626daa, 0x89933677a85444b7);
            private static readonly UInt128 Int16HashSeed = UInt128.Create(0xe7a19001d3211c09, 0x33e0ba9fb8bc7940);
            private static readonly UInt128 Int32HashSeed = UInt128.Create(0x0320dc908e0d3e71, 0xf575de218f09ffa5);
            private static readonly UInt128 Int64HashSeed = UInt128.Create(0xed93baf7fdc76638, 0x0d5733c37e079869);
            private static readonly UInt128 UInt32HashSeed = UInt128.Create(0x78c441a2d2e9bb6e, 0xac88cb880ccda71d);

            private CosmosNumberHasher()
            {
                // Private constructor, since this class is a singleton.
            }

            public UInt128 Visit(CosmosFloat32 cosmosFloat32, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Float32HashSeed, seed);
                float value = cosmosFloat32.GetValue();

                // Normalize 0.0f and -0.0f value
                // https://stackoverflow.com/questions/3139538/is-minus-zero-0-equivalent-to-zero-0-in-c-sharp
                if (value == 0.0f)
                {
                    value = 0;
                }

                hash = MurmurHash3.Hash128((UInt128)BitConverter.DoubleToInt64Bits(value), hash);
                return hash;
            }

            public UInt128 Visit(CosmosFloat64 cosmosFloat64, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Float64HashSeed, seed);
                double value = cosmosFloat64.GetValue();

                // Normalize 0.0 and -0.0 value
                // https://stackoverflow.com/questions/3139538/is-minus-zero-0-equivalent-to-zero-0-in-c-sharp
                if (value == 0.0)
                {
                    value = 0;
                }

                hash = MurmurHash3.Hash128((UInt128)BitConverter.DoubleToInt64Bits(value), hash);
                return hash;
            }

            public UInt128 Visit(CosmosInt16 cosmosInt16, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Int16HashSeed, seed);
                short value = cosmosInt16.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public UInt128 Visit(CosmosInt32 cosmosInt32, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Int32HashSeed, seed);
                int value = cosmosInt32.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public UInt128 Visit(CosmosInt64 cosmosInt64, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Int64HashSeed, seed);
                long value = cosmosInt64.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public UInt128 Visit(CosmosInt8 cosmosInt8, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Int8HashSeed, seed);
                sbyte value = cosmosInt8.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }

            public UInt128 Visit(CosmosNumber64 cosmosNumber64, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.Number64HashSeed, seed);
                Number64 value = cosmosNumber64.GetValue();
                Span<byte> buffer = stackalloc byte[Number64.SizeOf];
                value.CopyTo(buffer);
                return MurmurHash3.Hash128(buffer, hash);
            }

            public UInt128 Visit(CosmosUInt32 cosmosUInt32, UInt128 seed)
            {
                UInt128 hash = MurmurHash3.Hash128(CosmosNumberHasher.UInt32HashSeed, seed);
                uint value = cosmosUInt32.GetValue();
                hash = MurmurHash3.Hash128(value, hash);
                return hash;
            }
        }
    }
}
