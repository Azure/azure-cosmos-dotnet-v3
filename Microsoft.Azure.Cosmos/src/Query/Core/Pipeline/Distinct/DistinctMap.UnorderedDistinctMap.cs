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
        private sealed class UnorderdDistinctMap : DistinctMap
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
            /// Stores all the simple values that we don't want to dedicate a hash set for.
            /// </summary>
            private SimpleValues simpleValues;

            private UnorderdDistinctMap(
                HashSet<Number64> numbers,
                HashSet<uint> stringsLength4,
                HashSet<ulong> stringsLength8,
                HashSet<UInt128> stringsLength16,
                HashSet<UInt128> stringsLength16Plus,
                HashSet<UInt128> arrays,
                HashSet<UInt128> objects,
                SimpleValues simpleValues)
            {
                this.numbers = numbers ?? throw new ArgumentNullException(nameof(numbers));
                this.stringsLength4 = stringsLength4 ?? throw new ArgumentNullException(nameof(stringsLength4));
                this.stringsLength8 = stringsLength8 ?? throw new ArgumentNullException(nameof(stringsLength8));
                this.stringsLength16 = stringsLength16 ?? throw new ArgumentNullException(nameof(stringsLength16));
                this.stringsLength16Plus = stringsLength16Plus ?? throw new ArgumentNullException(nameof(stringsLength16Plus));
                this.arrays = arrays ?? throw new ArgumentNullException(nameof(arrays));
                this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
                this.simpleValues = simpleValues;
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
                return cosmosElement switch
                {
                    CosmosArray cosmosArray => this.AddArrayValue(cosmosArray),
                    CosmosBoolean cosmosBoolean => this.AddSimpleValue(cosmosBoolean.Value ? SimpleValues.True : SimpleValues.False),
                    CosmosNull _ => this.AddSimpleValue(SimpleValues.Null),
                    CosmosNumber cosmosNumber => this.AddNumberValue(cosmosNumber.Value),
                    CosmosObject cosmosObject => this.AddObjectValue(cosmosObject),
                    CosmosString cosmosString => this.AddStringValue(cosmosString.Value),
                    _ => throw new ArgumentOutOfRangeException($"Unexpected {nameof(CosmosElement)}: {cosmosElement}"),
                };
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
                        UnorderdDistinctMap.PropertyNames.Numbers,
                        CosmosArray.Create(this.numbers.Select(x => CosmosNumber64.Create(x)))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.StringsLength4,
                        CosmosArray.Create(this.stringsLength4.Select(x => CosmosUInt32.Create(x)))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.StringsLength8,
                        CosmosArray.Create(this.stringsLength8.Select(x => CosmosInt64.Create((long)x)))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.StringsLength16,
                        CosmosArray.Create(this.stringsLength16.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.StringsLength16Plus,
                        CosmosArray.Create(this.stringsLength16Plus.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.Arrays,
                        CosmosArray.Create(this.arrays.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.Object,
                        CosmosArray.Create(this.objects.Select(x => CosmosBinary.Create(UInt128.ToByteArray(x))))
                    },
                    {
                        UnorderdDistinctMap.PropertyNames.SimpleValues,
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
                if (utf8Length <= UnorderdDistinctMap.UInt128Length)
                {
                    Span<byte> utf8Buffer = stackalloc byte[UInt128Length];
                    Encoding.UTF8.GetBytes(value, utf8Buffer); 
                    if (utf8Length == 0)
                    {
                        added = this.AddSimpleValue(SimpleValues.EmptyString);
                    }
                    else if (utf8Length <= UnorderdDistinctMap.UIntLength)
                    {
                        uint uintValue = MemoryMarshal.Read<uint>(utf8Buffer);
                        added = this.stringsLength4.Add(uintValue);
                    }
                    else if (utf8Length <= UnorderdDistinctMap.ULongLength)
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

            public static TryCatch<DistinctMap> TryCreate(CosmosElement continuationToken)
            {
                HashSet<Number64> numbers = new HashSet<Number64>();
                HashSet<uint> stringsLength4 = new HashSet<uint>();
                HashSet<ulong> stringsLength8 = new HashSet<ulong>();
                HashSet<UInt128> stringsLength16 = new HashSet<UInt128>();
                HashSet<UInt128> stringsLength16Plus = new HashSet<UInt128>();
                HashSet<UInt128> arrays = new HashSet<UInt128>();
                HashSet<UInt128> objects = new HashSet<UInt128>();
                SimpleValues simpleValues = SimpleValues.None;

                if (continuationToken != null)
                {
                    if (!(continuationToken is CosmosObject hashDictionary))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    // Numbers
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.PropertyNames.Numbers, out CosmosArray numbersArray))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawNumber in numbersArray)
                    {
                        if (!(rawNumber is CosmosNumber64 number))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                        }

                        numbers.Add(number.GetValue());
                    }

                    // Strings Length 4
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.PropertyNames.StringsLength4, out CosmosArray stringsLength4Array))
                    {
                        return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawStringLength4 in stringsLength4Array)
                    {
                        if (!(rawStringLength4 is CosmosUInt32 stringlength4))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                        }

                        stringsLength4.Add(stringlength4.GetValue());
                    }

                    // Strings Length 8
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.PropertyNames.StringsLength8, out CosmosArray stringsLength8Array))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    foreach (CosmosElement rawStringLength8 in stringsLength8Array)
                    {
                        if (!(rawStringLength8 is CosmosInt64 stringlength8))
                        {
                            return TryCatch<DistinctMap>.FromException(
                                new MalformedContinuationTokenException(
                                    $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                        }

                        stringsLength8.Add((ulong)stringlength8.GetValue());
                    }

                    // Strings Length 16
                    stringsLength16 = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.PropertyNames.StringsLength16);

                    // Strings Length 24
                    stringsLength16Plus = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.PropertyNames.StringsLength16Plus);

                    // Array
                    arrays = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.PropertyNames.Arrays);

                    // Object
                    objects = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.PropertyNames.Object);

                    // Simple Values
                    CosmosElement rawSimpleValues = hashDictionary[UnorderdDistinctMap.PropertyNames.SimpleValues];
                    if (!(rawSimpleValues is CosmosString simpleValuesString))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    if (!Enum.TryParse<SimpleValues>(simpleValuesString.Value, out simpleValues))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }
                }

                return TryCatch<DistinctMap>.FromResult(new UnorderdDistinctMap(
                    numbers,
                    stringsLength4,
                    stringsLength8,
                    stringsLength16,
                    stringsLength16Plus,
                    arrays,
                    objects,
                    simpleValues));
            }

            private static HashSet<UInt128> Parse128BitHashes(CosmosObject hashDictionary, string propertyName)
            {
                HashSet<UInt128> hashSet = new HashSet<UInt128>();
                if (!hashDictionary.TryGetValue(propertyName, out CosmosArray array))
                {
                    throw new MalformedContinuationTokenException(
                        $"{nameof(UnorderdDistinctMap)} continuation token was malformed.");
                }

                foreach (CosmosElement item in array)
                {
                    if (!(item is CosmosBinary binary))
                    {
                        throw new MalformedContinuationTokenException(
                            $"{nameof(UnorderdDistinctMap)} continuation token was malformed.");
                    }

                    UInt128 uint128 = UInt128.FromByteArray(binary.Value.Span);
                    hashSet.Add(uint128);
                }

                return hashSet;
            }
        }
    }
}
