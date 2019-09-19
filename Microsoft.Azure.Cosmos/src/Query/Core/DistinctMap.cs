//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Base class for all types of DistinctMaps.
    /// An IDistinctMap is able to efficiently store a hash set of json values.
    /// This is done by taking the json value and storing a GUID like hash of that value in a hashset.
    /// By storing the hash we avoid storing the entire object in main memory.
    /// Only downside is that there is a possibility of a hash collision.
    /// However we store the hash as 192 bits, so the possibility of a collision is pretty low.
    /// You can run the birthday paradox math to figure out how low: https://en.wikipedia.org/wiki/Birthday_problem
    /// </summary>
    internal abstract partial class DistinctMap
    {
        /// <summary>
        /// Creates an IDistinctMap based on the type.
        /// </summary>
        /// <param name="distinctQueryType">The type of distinct query.</param>
        /// <param name="previousHash">The hash of the previous value successfully inserted into this DistinctMap</param>
        /// <returns>The appropriate IDistinctMap.</returns>
        public static DistinctMap Create(DistinctQueryType distinctQueryType, UInt192? previousHash)
        {
            switch (distinctQueryType)
            {
                case DistinctQueryType.None:
                    throw new ArgumentException("distinctQueryType can not be None. This part of code is not supposed to be reachable. Please contact support to resolve this issue.");
                case DistinctQueryType.Unordered:
                    return new UnorderdDistinctMap();
                case DistinctQueryType.Ordered:
                    return new OrderedDistinctMap(previousHash.GetValueOrDefault());
                default:
                    throw new ArgumentException($"Unrecognized DistinctQueryType: {distinctQueryType}.");
            }
        }

        /// <summary>
        /// Adds a JToken to this DistinctMap.
        /// </summary>
        /// <param name="cosmosElement">The element to add.</param>
        /// <param name="hash">The hash of the token.</param>
        /// <returns>Whether or not the token was successfully added.</returns>
        public abstract bool Add(CosmosElement cosmosElement, out UInt192? hash);

        /// <summary>
        /// Gets the hash of a JToken.
        /// </summary>
        /// <param name="cosmosElement">The token to hash.</param>
        /// <returns>The hash of the JToken.</returns>
        protected static UInt192 GetHash(CosmosElement cosmosElement)
        {
            return DistinctHash.Value.GetHashToken(cosmosElement);
        }

        /// <summary>
        /// Base class for DistinctHash.
        /// This class is able to take hashes with seeded values.
        /// </summary>
        private sealed class DistinctHash
        {
            /// <summary>
            /// Singleton for DistinctHash.
            /// </summary>
            /// <remarks>All the hashseeds have to be different.</remarks>
            public static readonly DistinctHash Value = new DistinctHash(
                new HashSeeds(
                    rootHashSeed: UInt192.Create(0xddef2a91418ae4ea, 0x9fcd55808a038bf3, 0x3e2614f44255738b),
                    nullHashSeed: UInt192.Create(0xaa64e7e7095c45ed, 0xbc660e452fdf5c95, 0xaba2a6ee2816dc2a),
                    falseHashSeed: UInt192.Create(0x13d74df965a60011, 0xa75872ce21865e72, 0xd31fdca229b8cd24),
                    trueHashSeed: UInt192.Create(0x4272616e646f6e20, 0x532043686f6e6700, 0x5fe6eb50c7b537a9),
                    numberHashSeed: UInt192.Create(0xb682f02a12588b1d, 0x0ed0c0611dd274e7, 0x58f316f4543f1456),
                    stringHashSeed: UInt192.Create(0x6807e00c508a5263, 0x0961f2815ea02dd2, 0xad47f5a77ee28667),
                    arrayHashSeed: UInt192.Create(0xe8dd264ef643c77b, 0x6340f4f895f86fb3, 0xe82c32939e1aa7c1),
                    objectHashSeed: UInt192.Create(0x27ae100c664520b1, 0xb3e8b5e578a281bf, 0x6e53db80f59dbb3a),
                    arrayIndexHashSeed: UInt192.Create(0xf38a8aaa4c3089d1, 0x5f693b1bd7fb6cee, 0xc310c21dc865e342),
                    propertyNameHashSeed: UInt192.Create(0xff6be1e2b9304754, 0xcd01ae19ec6204f5, 0x47b889211c290322)));

            /// <summary>
            /// Length of a UInt192 in bits
            /// </summary>
            private const int UInt192LengthInBits = 192;

            /// <summary>
            /// The number of bits in a byte.
            /// </summary>
            private const int BitsPerByte = 8;

            /// <summary>
            /// Length of a UInt192 in bytes.
            /// </summary>
            private const int UInt192LengthInBytes = UInt192LengthInBits / BitsPerByte;

            /// <summary>
            /// Initializes a new instance of the DistinctHash class.
            /// </summary>
            /// <param name="hashSeeds">The hash seeds to use.</param>
            private DistinctHash(HashSeeds hashSeeds)
            {
                this.HashSeedValues = hashSeeds;
            }

            /// <summary>
            /// Gets the HashSeeds for this type.
            /// </summary>
            public HashSeeds HashSeedValues
            {
                get;
            }

            /// <summary>
            /// Gets the hash given a value and a seed.
            /// </summary>
            /// <param name="value">The value to hash.</param>
            /// <param name="seed">The seed.</param>
            /// <returns>The hash.</returns>
            public UInt192 GetHash(UInt192 value, UInt192 seed)
            {
                return this.GetHash(UInt192.ToByteArray(value), seed);
            }

            /// <summary>
            /// Gets the hash of a byte array.
            /// </summary>
            /// <param name="bytes">The bytes.</param>
            /// <param name="seed">The seed.</param>
            /// <returns>The hash.</returns>
            public UInt192 GetHash(byte[] bytes, UInt192 seed)
            {
                UInt128 hash128 = MurmurHash3.Hash128(bytes, bytes.Length, UInt128.Create(seed.GetLow(), seed.GetMid()));
                ulong hash64 = MurmurHash3.Hash64(bytes, bytes.Length, seed.GetHigh());
                return UInt192.Create(hash128.GetLow(), hash128.GetHigh(), hash64);
            }

            /// <summary>
            /// Gets the hash of a JToken value.
            /// </summary>
            /// <param name="cosmosElement">The element to load.</param>
            /// <returns>The hash of the JToken.</returns>
            public UInt192 GetHashToken(CosmosElement cosmosElement)
            {
                return this.GetHashToken(cosmosElement, this.HashSeedValues.Root);
            }

            /// <summary>
            /// Gets the hash of a JToken given a seed.
            /// </summary>
            /// <param name="cosmosElement">The cosmos element to hash.</param>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of the JToken.</returns>
            private UInt192 GetHashToken(CosmosElement cosmosElement, UInt192 seed)
            {
                if (cosmosElement == null)
                {
                    return this.GetUndefinedHash(seed);
                }

                CosmosElementType cosmosElementType = cosmosElement.Type;
                UInt192 hash;
                switch (cosmosElementType)
                {
                    case CosmosElementType.Array:
                        hash = this.GetArrayHash(cosmosElement as CosmosArray, seed);
                        break;

                    case CosmosElementType.Boolean:
                        hash = this.GetBooleanHash((cosmosElement as CosmosBoolean).Value, seed);
                        break;

                    case CosmosElementType.Null:
                        hash = this.GetNullHash(seed);
                        break;

                    case CosmosElementType.Number:
                        CosmosNumber cosmosNumber = (cosmosElement as CosmosNumber);
                        double number;
                        if (cosmosNumber.IsFloatingPoint)
                        {
                            number = cosmosNumber.AsFloatingPoint().Value;
                        }
                        else
                        {
                            number = cosmosNumber.AsInteger().Value;
                        }

                        hash = this.GetNumberHash(number, seed);
                        break;

                    case CosmosElementType.Object:
                        hash = this.GetObjectHash(cosmosElement as CosmosObject, seed);
                        break;

                    case CosmosElementType.String:
                        hash = this.GetStringHash((cosmosElement as CosmosString).Value, seed);
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
            private UInt192 GetUndefinedHash(UInt192 seed)
            {
                return seed;
            }

            /// <summary>
            /// Gets the hash of a null JSON value.
            /// </summary>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of a null JSON value given a seed.</returns>
            private UInt192 GetNullHash(UInt192 seed)
            {
                return this.GetHash(this.HashSeedValues.Null, seed);
            }

            /// <summary>
            /// Gets the hash of a boolean JSON value.
            /// </summary>
            /// <param name="boolean">The boolean to hash.</param>
            /// <param name="seed">The seed.</param>
            /// <returns>The hash of a boolean JSON value.</returns>
            private UInt192 GetBooleanHash(bool boolean, UInt192 seed)
            {
                return this.GetHash(boolean ? this.HashSeedValues.True : this.HashSeedValues.False, seed);
            }

            /// <summary>
            /// Gets the hash of a JSON number value.
            /// </summary>
            /// <param name="number">The number to hash.</param>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of a JSON number value.</returns>
            private UInt192 GetNumberHash(double number, UInt192 seed)
            {
                UInt192 hash = this.GetHash(this.HashSeedValues.Number, seed);
                hash = this.GetHash((UInt192)BitConverter.DoubleToInt64Bits(number), hash);
                return hash;
            }

            /// <summary>
            /// Gets the hash of a JSON string value.
            /// </summary>
            /// <param name="value">The value to hash.</param>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of a JSON string value.</returns>
            private UInt192 GetStringHash(string value, UInt192 seed)
            {
                UInt192 hash = this.GetHash(this.HashSeedValues.String, seed);
                byte[] stringBytes = Encoding.UTF8.GetBytes(value);
                return this.GetHash(stringBytes, hash);
            }

            /// <summary>
            /// Gets the hash of a JSON array.
            /// </summary>
            /// <param name="cosmosArray">The array to hash.</param>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of a JSON array.</returns>
            private UInt192 GetArrayHash(CosmosArray cosmosArray, UInt192 seed)
            {
                // Start the array with a distinct hash, so that empty array doesn't hash to another value.
                UInt192 hash = this.GetHash(this.HashSeedValues.Array, seed);

                // Incorporate all the array items into the hash.
                for (int index = 0; index < cosmosArray.Count; index++)
                {
                    CosmosElement arrayItem = cosmosArray[index];

                    // Order of array items matter in equality check, so we add the index just to be safe.
                    // For now we know that murmurhash will correctly give a different hash for 
                    // [true, false, true] and [true, true, false]
                    // due to the way the seed works.
                    // But we add the index just incase that property does not hold in the future.
                    UInt192 arrayItemSeed = this.HashSeedValues.ArrayIndex + index;
                    hash = this.GetHash(hash, this.GetHashToken(arrayItem, arrayItemSeed));
                }

                return hash;
            }

            /// <summary>
            /// Gets the hash of a JSON object.
            /// </summary>
            /// <param name="cosmosObject">The object to hash.</param>
            /// <param name="seed">The seed to use.</param>
            /// <returns>The hash of a JSON object.</returns>
            private UInt192 GetObjectHash(CosmosObject cosmosObject, UInt192 seed)
            {
                // Start the object with a distinct hash, so that empty object doesn't hash to another value.
                UInt192 hash = this.GetHash(this.HashSeedValues.Object, seed);

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
                UInt192 intermediateHash = 0;

                // Property order should not result in a different hash.
                // This is consistent with equality comparison.
                foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
                {
                    UInt192 nameHash = this.GetHashToken(
                        CosmosString.Create(kvp.Key),
                        this.HashSeedValues.PropertyName);
                    UInt192 propertyHash = this.GetHashToken(kvp.Value, nameHash);

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
                    hash = this.GetHash(intermediateHash, hash);
                }

                return hash;
            }

            /// <summary>
            /// The seeds to use for hashing different json types.
            /// </summary>
            public struct HashSeeds
            {
                /// <summary>
                /// Initializes a new instance of the HashSeeds struct.
                /// </summary>
                /// <param name="rootHashSeed">The seed used for the JSON root.</param>
                /// <param name="nullHashSeed">The seed used for JSON null values.</param>
                /// <param name="falseHashSeed">The seed used for JSON false values.</param>
                /// <param name="trueHashSeed">The seed used for JSON true values.</param>
                /// <param name="numberHashSeed">The seed used for JSON number values.</param>
                /// <param name="stringHashSeed">The seed used for JSON string values.</param>
                /// <param name="arrayHashSeed">The seed used for JSON array values.</param>
                /// <param name="objectHashSeed">The seed used for JSON object values.</param>
                /// <param name="arrayIndexHashSeed">The seed used for JSON array elements.</param>
                /// <param name="propertyNameHashSeed">The seed used for JSON property names.</param>
                public HashSeeds(
                    UInt192 rootHashSeed,
                    UInt192 nullHashSeed,
                    UInt192 falseHashSeed,
                    UInt192 trueHashSeed,
                    UInt192 numberHashSeed,
                    UInt192 stringHashSeed,
                    UInt192 arrayHashSeed,
                    UInt192 objectHashSeed,
                    UInt192 arrayIndexHashSeed,
                    UInt192 propertyNameHashSeed)
                {
                    this.Root = rootHashSeed;
                    this.Null = nullHashSeed;
                    this.False = falseHashSeed;
                    this.True = trueHashSeed;
                    this.Number = numberHashSeed;
                    this.String = stringHashSeed;
                    this.Array = arrayHashSeed;
                    this.Object = objectHashSeed;
                    this.ArrayIndex = arrayIndexHashSeed;
                    this.PropertyName = propertyNameHashSeed;
                }

                /// <summary>
                /// Gets the seed used for the JSON root.
                /// </summary>
                public UInt192 Root { get; }

                /// <summary>
                /// Gets the seed used for JSON null values.
                /// </summary>
                public UInt192 Null { get; }

                /// <summary>
                /// Gets the seed used for JSON false values.
                /// </summary>
                public UInt192 False { get; }

                /// <summary>
                /// Gets the seed used for JSON true values.
                /// </summary>
                public UInt192 True { get; }

                /// <summary>
                /// Gets the seed used for JSON number values.
                /// </summary>
                public UInt192 Number { get; }

                /// <summary>
                /// Gets the seed used for JSON string values.
                /// </summary>
                public UInt192 String { get; }

                /// <summary>
                /// Gets the seed used for JSON array values.
                /// </summary>
                public UInt192 Array { get; }

                /// <summary>
                /// Gets the seed used for JSON object values.
                /// </summary>
                public UInt192 Object { get; }

                /// <summary>
                /// Gets the seed used for JSON array elements.
                /// </summary>
                public UInt192 ArrayIndex { get; }

                /// <summary>
                /// Gets the seed used for JSON property names.
                /// </summary>
                public UInt192 PropertyName { get; }
            }
        }
    }
}
