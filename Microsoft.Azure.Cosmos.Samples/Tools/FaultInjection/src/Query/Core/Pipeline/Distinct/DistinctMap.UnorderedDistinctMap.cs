//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Partial wrapper
    /// </summary>
    internal abstract partial class DistinctMap
    {
        /// <summary>
        /// Flags for all the simple json values, so that we don't need a separate hash for them.
        /// </summary>
        [Flags]
        private enum SimpleValues
        {
            /// <summary>
            /// None JSON Value.
            /// </summary>
            None = 0x0000,

            /// <summary>
            /// Undefined JSON Value.
            /// </summary>
            Undefined = 0x0001,

            /// <summary>
            /// Null JSON Value.
            /// </summary>
            Null = 0x0002,

            /// <summary>
            /// False JSON Value.
            /// </summary>
            False = 0x0004,

            /// <summary>
            /// True JSON Value.
            /// </summary>
            True = 0x0008,

            /// <summary>
            /// Empty String.
            /// </summary>
            EmptyString = 0x0010,

            /// <summary>
            /// Empty Array.
            /// </summary>
            EmptyArray = 0x0020,

            /// <summary>
            /// Empty Object.
            /// </summary>
            EmptyObject = 0x0040,
        }

        /// <summary>
        /// For distinct queries we need to keep a running hash set of all the documents seen.
        /// You can read more about this in DistinctDocumentQueryExecutionComponent.cs.
        /// This class does that with the additional optimization that it doesn't store the whole JSON.
        /// Instead this class takes a GUID like hash and store that instead.
        /// </summary>
        private sealed class UnorderedDistinctMap : DistinctMap
        {
            /// <summary>
            /// Length of UInt128 (in bytes).
            /// </summary>
            private const int UInt128Length = 16;

            /// <summary>
            /// Length of ulong (in bytes).
            /// </summary>
            private const int ULongLength = 8;

            /// <summary>
            /// Length of uint (in bytes).
            /// </summary>
            private const int UIntLength = 4;

            private static class PropertyNames
            {
                public const string Numbers = "Numbers";
                public const string StringsLength4 = "StringsLength4";
                public const string StringsLength8 = "StringsLength8";
                public const string StringsLength16 = "StringsLength16";
                public const string StringsLength16Plus = "StringsLength16+";
                public const string Arrays = "Arrays";
                public const string Object = "Object";
                public const string Guids = "Guids";
                public const string Blobs = "Blobs";
                public const string SimpleValues = "SimpleValues";
            }

            /// <summary>
            /// HashSet for all numbers seen.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<Number64> numbers;

            /// <summary>
            /// HashSet for all strings seen of length less than or equal to 4 stored as a uint.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<uint> stringsLength4;

            /// <summary>
            /// HashSet for all strings seen of length less than or equal to 8 stored as a ulong.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<ulong> stringsLength8;

            /// <summary>
            /// HashSet for all strings of length less than or equal to 16 stored as a UInt128.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<UInt128> stringsLength16;

            /// <summary>
            /// HashSet for all strings seen of length greater than 24 stored as a UInt192.
            /// This set only stores the hash, since we don't want to spend the space for storing large strings.
            /// </summary>
            private readonly HashSet<UInt128> stringsLength16Plus;

            /// <summary>
            /// HashSet for all arrays seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large arrays.
            /// </summary>
            private readonly HashSet<UInt128> arrays;

            /// <summary>
            /// HashSet for all object seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large objects.
            /// </summary>
            private readonly HashSet<UInt128> objects;

            /// <summary>
            /// HashSet for all CosmosGuids seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large CosmosGuids.
            /// </summary>
            private readonly HashSet<UInt128> guids;

            /// <summary>
            /// HashSet for all CosmosBinarys seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large CosmosBinary objects.
            /// </summary>
            private readonly HashSet<UInt128> blobs;

            /// <summary>
            /// Used to dispatch Add calls.
            /// </summary>
            private readonly CosmosElementVisitor visitor;

            /// <summary>
            /// Stores all the simple values that we don't want to dedicate a hash set for.
            /// </summary>
            private SimpleValues simpleValues;

            private UnorderedDistinctMap(
                HashSet<Number64> numbers,
                HashSet<uint> stringsLength4,
                HashSet<ulong> stringsLength8,
                HashSet<UInt128> stringsLength16,
                HashSet<UInt128> stringsLength16Plus,
                HashSet<UInt128> arrays,
                HashSet<UInt128> objects,
                HashSet<UInt128> guids,
                HashSet<UInt128> blobs,
                SimpleValues simpleValues)
            {
                this.numbers = numbers ?? throw new ArgumentNullException(nameof(numbers));
                this.stringsLength4 = stringsLength4 ?? throw new ArgumentNullException(nameof(stringsLength4));
                this.stringsLength8 = stringsLength8 ?? throw new ArgumentNullException(nameof(stringsLength8));
                this.stringsLength16 = stringsLength16 ?? throw new ArgumentNullException(nameof(stringsLength16));
                this.stringsLength16Plus = stringsLength16Plus ?? throw new ArgumentNullException(nameof(stringsLength16Plus));
                this.arrays = arrays ?? throw new ArgumentNullException(nameof(arrays));
                this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
                this.guids = guids ?? throw new ArgumentNullException(nameof(guids));
                this.blobs = blobs ?? throw new ArgumentNullException(nameof(blobs));
                this.simpleValues = simpleValues;
                this.visitor = new CosmosElementVisitor(this);
            }

            /// <summary>
            /// Adds a JToken to this map if it hasn't already been added.
            /// </summary>
            /// <param name="cosmosElement">The element to add.</param>
            /// <param name="hash">The hash of the token.</param>
            /// <returns>Whether or not the item was added to this Distinct Map.</returns>
            public override bool Add(CosmosElement cosmosElement, out UInt128 hash)
            {
                // Unordered distinct does not need to return a valid hash.
                // Since it doesn't need the last hash for a continuation.
                hash = default;
                return cosmosElement.Accept(this.visitor);
            }

            public override string GetContinuationToken()
            {
                return this.GetCosmosElementContinuationToken().ToString();
            }

            public override CosmosElement GetCosmosElementContinuationToken()
            {
                Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>()
                {
                    {
                        UnorderedDistinctMap.PropertyNames.Numbers,
                        CosmosArray.Create(this.numbers.Select(x => CosmosNumber64.Create(x)))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.StringsLength4,
                        CosmosArray.Create(this.stringsLength4.Select(x => CosmosUInt32.Create(x)))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.StringsLength8,
                        CosmosArray.Create(this.stringsLength8.Select(x => CosmosInt64.Create((long)x)))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.StringsLength16,
                        CosmosArray.Create(this.stringsLength16.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.StringsLength16Plus,
                        CosmosArray.Create(this.stringsLength16Plus.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.Arrays,
                        CosmosArray.Create(this.arrays.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.Object,
                        CosmosArray.Create(this.objects.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.Guids,
                        CosmosArray.Create(this.guids.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.Blobs,
                        CosmosArray.Create(this.blobs.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderedDistinctMap.PropertyNames.SimpleValues,
                        CosmosString.Create(this.simpleValues.ToString())
                    }
                };

                return CosmosObject.Create(dictionary);
            }

            /// <summary>
            /// Adds a number value to the map.
            /// </summary>
            /// <param name="value">The value to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddNumberValue(Number64 value)
            {
                return this.numbers.Add(value);
            }

            /// <summary>
            /// Adds a simple value to the map.
            /// </summary>
            /// <param name="value">The simple value.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddSimpleValue(SimpleValues value)
            {
                if (((int)this.simpleValues & (int)value) == 0)
                {
                    this.simpleValues = (SimpleValues)((int)this.simpleValues | (int)value);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Adds a string to the distinct map.
            /// </summary>
            /// <param name="value">The string to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddStringValue(string value)
            {
                bool added = false;
                int utf8Length = Encoding.UTF8.GetByteCount(value);

                // If you can fit the string with full fidelity in 16 bytes, then you might as well just hash the string itself.
                if (utf8Length <= UnorderedDistinctMap.UInt128Length)
                {
                    Span<byte> utf8Buffer = stackalloc byte[UInt128Length];
                    Encoding.UTF8.GetBytes(value, utf8Buffer);
                    if (utf8Length == 0)
                    {
                        added = this.AddSimpleValue(SimpleValues.EmptyString);
                    }
                    else if (utf8Length <= UnorderedDistinctMap.UIntLength)
                    {
                        uint uintValue = MemoryMarshal.Read<uint>(utf8Buffer);
                        added = this.stringsLength4.Add(uintValue);
                    }
                    else if (utf8Length <= UnorderedDistinctMap.ULongLength)
                    {
                        ulong uLongValue = MemoryMarshal.Read<ulong>(utf8Buffer);
                        added = this.stringsLength8.Add(uLongValue);
                    }
                    else
                    {
                        UInt128 uInt128Value = UInt128.FromByteArray(utf8Buffer);
                        added = this.stringsLength16.Add(uInt128Value);
                    }
                }
                else
                {
                    // Else the string is too large and we will just store the hash.
                    UInt128 uint128Value = DistinctHash.GetHash(CosmosString.Create(value));
                    added = this.stringsLength16Plus.Add(uint128Value);
                }

                return added;
            }

            /// <summary>
            /// Adds an array value to the distinct map.
            /// </summary>
            /// <param name="array">The array to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddArrayValue(CosmosArray array)
            {
                UInt128 hash = DistinctHash.GetHash(array);
                return this.arrays.Add(hash);
            }

            /// <summary>
            /// Adds an object value to the distinct map.
            /// </summary>
            /// <param name="cosmosObject">The object to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddObjectValue(CosmosObject cosmosObject)
            {
                UInt128 hash = DistinctHash.GetHash(cosmosObject);
                return this.objects.Add(hash);
            }

            /// <summary>
            /// Adds a guid value to the distinct map.
            /// </summary>
            /// <param name="guid">The guid to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddGuidValue(CosmosGuid guid)
            {
                UInt128 hash = DistinctHash.GetHash(guid);
                return this.guids.Add(hash);
            }

            /// <summary>
            /// Adds a binary value to the distinct map.
            /// </summary>
            /// <param name="binary">The array to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddBinaryValue(CosmosBinary binary)
            {
                UInt128 hash = DistinctHash.GetHash(binary);
                return this.blobs.Add(hash);
            }

            public static TryCatch<DistinctMap> TryCreate(CosmosElement continuationToken)
            {
                HashSet<Number64> numbers = new HashSet<Number64>();
                HashSet<uint> stringsLength4 = new HashSet<uint>();
                HashSet<ulong> stringsLength8 = new HashSet<ulong>();
                HashSet<UInt128> stringsLength16 = new HashSet<UInt128>();
                HashSet<UInt128> stringsLength16Plus = new HashSet<UInt128>();
                HashSet<UInt128> arrays = new HashSet<UInt128>();
                HashSet<UInt128> objects = new HashSet<UInt128>();
                HashSet<UInt128> guids = new HashSet<UInt128>();
                HashSet<UInt128> blobs = new HashSet<UInt128>();
                SimpleValues simpleValues = SimpleValues.None;

                if (continuationToken != null)
                {
                    if (!(continuationToken is CosmosObject hashDictionary))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }

                    // Numbers
                    if (!hashDictionary.TryGetValue(UnorderedDistinctMap.PropertyNames.Numbers, out CosmosArray numbersArray))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawNumber in numbersArray)
                    {
                        if (!(rawNumber is CosmosNumber64 number))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                        }

                        numbers.Add(number.GetValue());
                    }

                    // Strings Length 4
                    if (!hashDictionary.TryGetValue(UnorderedDistinctMap.PropertyNames.StringsLength4, out CosmosArray stringsLength4Array))
                    {
                        return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawStringLength4 in stringsLength4Array)
                    {
                        if (!(rawStringLength4 is CosmosUInt32 stringlength4))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                        }

                        stringsLength4.Add(stringlength4.GetValue());
                    }

                    // Strings Length 8
                    if (!hashDictionary.TryGetValue(UnorderedDistinctMap.PropertyNames.StringsLength8, out CosmosArray stringsLength8Array))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawStringLength8 in stringsLength8Array)
                    {
                        if (!(rawStringLength8 is CosmosInt64 stringlength8))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                        }

                        stringsLength8.Add((ulong)stringlength8.GetValue());
                    }

                    stringsLength16 = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.StringsLength16);

                    stringsLength16Plus = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.StringsLength16Plus);

                    arrays = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.Arrays);

                    objects = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.Object);

                    guids = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.Guids);

                    blobs = Parse128BitHashes(hashDictionary, UnorderedDistinctMap.PropertyNames.Blobs);

                    // Simple Values
                    CosmosElement rawSimpleValues = hashDictionary[UnorderedDistinctMap.PropertyNames.SimpleValues];
                    if (!(rawSimpleValues is CosmosString simpleValuesString))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }

                    if (!Enum.TryParse<SimpleValues>(simpleValuesString.Value, out simpleValues))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderedDistinctMap)} continuation token was malformed."));
                    }
                }

                return TryCatch<DistinctMap>.FromResult(new UnorderedDistinctMap(
                    numbers,
                    stringsLength4,
                    stringsLength8,
                    stringsLength16,
                    stringsLength16Plus,
                    arrays,
                    objects,
                    guids,
                    blobs,
                    simpleValues));
            }

            private static HashSet<UInt128> Parse128BitHashes(CosmosObject hashDictionary, string propertyName)
            {
                HashSet<UInt128> hashSet = new HashSet<UInt128>();
                if (!hashDictionary.TryGetValue(propertyName, out CosmosArray array))
                {
                    throw new MalformedContinuationTokenException(
                        $"{nameof(UnorderedDistinctMap)} continuation token was malformed.");
                }

                foreach (CosmosElement item in array)
                {
                    if (!(item is CosmosBinary binary))
                    {
                        throw new MalformedContinuationTokenException(
                            $"{nameof(UnorderedDistinctMap)} continuation token was malformed.");
                    }

                    UInt128 uint128 = UInt128.FromByteArray(binary.Value.Span);
                    hashSet.Add(uint128);
                }

                return hashSet;
            }

            private sealed class CosmosElementVisitor : ICosmosElementVisitor<bool>
            {
                private readonly UnorderedDistinctMap parent;

                public CosmosElementVisitor(UnorderedDistinctMap parent)
                {
                    this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
                }

                public bool Visit(CosmosArray cosmosArray)
                {
                    return this.parent.AddArrayValue(cosmosArray);
                }

                public bool Visit(CosmosBinary cosmosBinary)
                {
                    return this.parent.AddBinaryValue(cosmosBinary);
                }

                public bool Visit(CosmosBoolean cosmosBoolean)
                {
                    return this.parent.AddSimpleValue(cosmosBoolean.Value ? SimpleValues.True : SimpleValues.False);
                }

                public bool Visit(CosmosGuid cosmosGuid)
                {
                    return this.parent.AddGuidValue(cosmosGuid);
                }

                public bool Visit(CosmosNull cosmosNull)
                {
                    return this.parent.AddSimpleValue(SimpleValues.Null);
                }

                public bool Visit(CosmosNumber cosmosNumber)
                {
                    return this.parent.AddNumberValue(cosmosNumber.Value);
                }

                public bool Visit(CosmosObject cosmosObject)
                {
                    return this.parent.AddObjectValue(cosmosObject);
                }

                public bool Visit(CosmosString cosmosString)
                {
                    return this.parent.AddStringValue(cosmosString.Value);
                }

                public bool Visit(CosmosUndefined cosmosUndefined)
                {
                    return this.parent.AddSimpleValue(SimpleValues.Undefined);
                }
            }
        }
    }
}
