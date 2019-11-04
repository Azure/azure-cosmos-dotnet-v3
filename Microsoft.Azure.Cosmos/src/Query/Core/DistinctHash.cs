// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Base class for DistinctHash.
    /// This class is able to take hashes with seeded values.
    /// </summary>
    internal static class DistinctHash
    {
        private static readonly UInt128 RootHashSeed = UInt128.Create(
            0xddef2a91418ae4ea,
            0x9fcd55808a038bf3);

        private static readonly UInt128 NullHashSeed = UInt128.Create(
            0xaa64e7e7095c45ed,
            0xbc660e452fdf5c95);

        private static readonly UInt128 FalseHashSeed = UInt128.Create(
            0x13d74df965a60011,
            0xa75872ce21865e72);

        private static readonly UInt128 TrueHashSeed = UInt128.Create(
            0x4272616e646f6e20,
            0x532043686f6e6700);

        private static readonly UInt128 NumberHashSeed = UInt128.Create(
            0xb682f02a12588b1d,
            0x0ed0c0611dd274e7);

        private static readonly UInt128 StringHashSeed = UInt128.Create(
            0x6807e00c508a5263,
            0x0961f2815ea02dd2);

        private static readonly UInt128 ArrayHashSeed = UInt128.Create(
            0xe8dd264ef643c77b,
            0x6340f4f895f86fb3);

        private static readonly UInt128 ObjectHashSeed = UInt128.Create(
            0x27ae100c664520b1,
            0xb3e8b5e578a281bf);

        private static readonly UInt128 ArrayIndexHashSeed = UInt128.Create(
            0xf38a8aaa4c3089d1,
            0x5f693b1bd7fb6cee);

        private static readonly UInt128 PropertyNameHashSeed = UInt128.Create(
            0xff6be1e2b9304754,
            0xcd01ae19ec6204f5);

        /// <summary>
        /// Gets the hash given a value and a seed.
        /// </summary>
        /// <param name="value">The value to hash.</param>
        /// <param name="seed">The seed.</param>
        /// <returns>The hash.</returns>
        public static UInt128 GetHash(UInt128 value, UInt128 seed)
        {
            return DistinctHash.GetHash(UInt128.ToByteArray(value), seed);
        }

        /// <summary>
        /// Gets the hash of a byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="seed">The seed.</param>
        /// <returns>The hash.</returns>
        public static UInt128 GetHash(byte[] bytes, UInt128 seed)
        {
            // TODO: Have MurmurHash3 work on Span<T> instead.
            Microsoft.Azure.Documents.UInt128 hash128 = Microsoft.Azure.Documents.Routing.MurmurHash3.Hash128(
                bytes,
                bytes.Length,
                Microsoft.Azure.Documents.UInt128.Create(seed.GetLow(), seed.GetHigh()));
            return UInt128.Create(hash128.GetLow(), hash128.GetHigh());
        }

        /// <summary>
        /// Gets the hash of a JToken value.
        /// </summary>
        /// <param name="cosmosElement">The element to load.</param>
        /// <returns>The hash of the JToken.</returns>
        public static UInt128 GetHash(CosmosElement cosmosElement)
        {
            return DistinctHash.GetHash(cosmosElement, DistinctHash.RootHashSeed);
        }

        /// <summary>
        /// Gets the hash of a JToken given a seed.
        /// </summary>
        /// <param name="cosmosElement">The cosmos element to hash.</param>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of the JToken.</returns>
        private static UInt128 GetHash(CosmosElement cosmosElement, UInt128 seed)
        {
            if (cosmosElement == null)
            {
                return DistinctHash.GetUndefinedHash(seed);
            }

            CosmosElementType cosmosElementType = cosmosElement.Type;
            UInt128 hash;
            switch (cosmosElementType)
            {
                case CosmosElementType.Array:
                    hash = DistinctHash.GetArrayHash(cosmosElement as CosmosArray, seed);
                    break;

                case CosmosElementType.Boolean:
                    hash = DistinctHash.GetBooleanHash((cosmosElement as CosmosBoolean).Value, seed);
                    break;

                case CosmosElementType.Null:
                    hash = DistinctHash.GetNullHash(seed);
                    break;

                case CosmosElementType.Number:
                    // TODO: we need to differentiate between the different number types.
                    CosmosNumber cosmosNumber = cosmosElement as CosmosNumber;
                    double number;
                    if (cosmosNumber.IsFloatingPoint)
                    {
                        number = cosmosNumber.AsFloatingPoint().Value;
                    }
                    else
                    {
                        number = cosmosNumber.AsInteger().Value;
                    }

                    hash = DistinctHash.GetNumberHash(number, seed);
                    break;

                case CosmosElementType.Object:
                    hash = DistinctHash.GetObjectHash(cosmosElement as CosmosObject, seed);
                    break;

                case CosmosElementType.String:
                    hash = DistinctHash.GetStringHash((cosmosElement as CosmosString).Value, seed);
                    break;

                default:
                    throw new ArgumentException($"Unexpected {nameof(CosmosElementType)} : {cosmosElementType}");
            }

            return hash;
        }

        /// <summary>
        /// Gets the hash of a undefined JSON value.
        /// </summary>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a undefined JSON value.</returns>
        private static UInt128 GetUndefinedHash(UInt128 seed)
        {
            return seed;
        }

        /// <summary>
        /// Gets the hash of a null JSON value.
        /// </summary>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a null JSON value given a seed.</returns>
        private static UInt128 GetNullHash(UInt128 seed)
        {
            return DistinctHash.GetHash(DistinctHash.NullHashSeed, seed);
        }

        /// <summary>
        /// Gets the hash of a boolean JSON value.
        /// </summary>
        /// <param name="boolean">The boolean to hash.</param>
        /// <param name="seed">The seed.</param>
        /// <returns>The hash of a boolean JSON value.</returns>
        private static UInt128 GetBooleanHash(bool boolean, UInt128 seed)
        {
            return DistinctHash.GetHash(boolean ? DistinctHash.TrueHashSeed : DistinctHash.FalseHashSeed, seed);
        }

        /// <summary>
        /// Gets the hash of a JSON number value.
        /// </summary>
        /// <param name="number">The number to hash.</param>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a JSON number value.</returns>
        private static UInt128 GetNumberHash(double number, UInt128 seed)
        {
            UInt128 hash = DistinctHash.GetHash(DistinctHash.NumberHashSeed, seed);
            hash = DistinctHash.GetHash((UInt128)BitConverter.DoubleToInt64Bits(number), hash);
            return hash;
        }

        /// <summary>
        /// Gets the hash of a JSON string value.
        /// </summary>
        /// <param name="value">The value to hash.</param>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a JSON string value.</returns>
        private static UInt128 GetStringHash(string value, UInt128 seed)
        {
            UInt128 hash = DistinctHash.GetHash(DistinctHash.StringHashSeed, seed);
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            return DistinctHash.GetHash(stringBytes, hash);
        }

        /// <summary>
        /// Gets the hash of a JSON array.
        /// </summary>
        /// <param name="cosmosArray">The array to hash.</param>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a JSON array.</returns>
        private static UInt128 GetArrayHash(CosmosArray cosmosArray, UInt128 seed)
        {
            // Start the array with a distinct hash, so that empty array doesn't hash to another value.
            UInt128 hash = DistinctHash.GetHash(DistinctHash.ArrayHashSeed, seed);

            // Incorporate all the array items into the hash.
            for (int index = 0; index < cosmosArray.Count; index++)
            {
                CosmosElement arrayItem = cosmosArray[index];

                // Order of array items matter in equality check, so we add the index just to be safe.
                // For now we know that murmurhash will correctly give a different hash for 
                // [true, false, true] and [true, true, false]
                // due to the way the seed works.
                // But we add the index just incase that property does not hold in the future.
                UInt128 arrayItemSeed = DistinctHash.ArrayIndexHashSeed + index;
                hash = DistinctHash.GetHash(hash, DistinctHash.GetHash(arrayItem, arrayItemSeed));
            }

            return hash;
        }

        /// <summary>
        /// Gets the hash of a JSON object.
        /// </summary>
        /// <param name="cosmosObject">The object to hash.</param>
        /// <param name="seed">The seed to use.</param>
        /// <returns>The hash of a JSON object.</returns>
        private static UInt128 GetObjectHash(CosmosObject cosmosObject, UInt128 seed)
        {
            // Start the object with a distinct hash, so that empty object doesn't hash to another value.
            UInt128 hash = DistinctHash.GetHash(DistinctHash.ObjectHashSeed, seed);

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
                UInt128 nameHash = DistinctHash.GetHash(
                    CosmosString.Create(kvp.Key),
                    DistinctHash.PropertyNameHashSeed);
                UInt128 propertyHash = DistinctHash.GetHash(kvp.Value, nameHash);

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
                hash = DistinctHash.GetHash(intermediateHash, hash);
            }

            return hash;
        }
    }
}
