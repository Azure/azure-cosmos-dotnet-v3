//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
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

            private const string NumbersName = "Numbers";
            private const string StringsLength4Name = "StringsLength4";
            private const string StringsLength8Name = "StringsLength8";
            private const string StringsLength16Name = "StringsLength16";
            private const string StringsLength16PlusName = "StringsLength16+";
            private const string ArraysName = "Arrays";
            private const string ObjectName = "Object";
            private const string SimpleValuesName = "SimpleValues";

            /// <summary>
            /// Buffer that gets reused to convert a .net string (utf-16) to a (utf-8) byte array.
            /// </summary>
            private readonly byte[] utf8Buffer;

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
                this.utf8Buffer = new byte[UnorderdDistinctMap.UInt128Length];
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
                bool added = false;
                CosmosElementType cosmosElementType = cosmosElement.Type;
                switch (cosmosElementType)
                {
                    case CosmosElementType.Array:
                        added = this.AddArrayValue(cosmosElement as CosmosArray);
                        break;

                    case CosmosElementType.Boolean:
                        added = this.AddSimpleValue((cosmosElement as CosmosBoolean).Value ? SimpleValues.True : SimpleValues.False);
                        break;

                    case CosmosElementType.Null:
                        added = this.AddSimpleValue(SimpleValues.Null);
                        break;

                    case CosmosElementType.Number:
                        CosmosNumber cosmosNumber = cosmosElement as CosmosNumber;
                        added = this.AddNumberValue(cosmosNumber.Value);
                        break;

                    case CosmosElementType.Object:
                        added = this.AddObjectValue(cosmosElement as CosmosObject);
                        break;

                    case CosmosElementType.String:
                        added = this.AddStringValue((cosmosElement as CosmosString).Value);
                        break;

                    default:
                        throw new ArgumentException($"Unexpected {nameof(CosmosElementType)}: {cosmosElementType}");
                }

                return added;
            }

            public override string GetContinuationToken()
            {
                IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
                jsonWriter.WriteObjectStart();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.NumbersName);
                jsonWriter.WriteArrayStart();
                foreach (Number64 number in this.numbers)
                {
                    jsonWriter.WriteNumberValue(number);
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.StringsLength4Name);
                jsonWriter.WriteArrayStart();
                foreach (uint stringLength4 in this.stringsLength4)
                {
                    jsonWriter.WriteUInt32Value(stringLength4);
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.StringsLength8Name);
                jsonWriter.WriteArrayStart();
                foreach (ulong stringLength8 in this.stringsLength8)
                {
                    jsonWriter.WriteInt64Value((long)stringLength8);
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.StringsLength16Name);
                jsonWriter.WriteArrayStart();
                foreach (UInt128 stringLength16 in this.stringsLength16)
                {
                    jsonWriter.WriteBinaryValue(UInt128.ToByteArray(stringLength16));
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.StringsLength16PlusName);
                jsonWriter.WriteArrayStart();
                foreach (UInt128 stringLength16Plus in this.stringsLength16Plus)
                {
                    jsonWriter.WriteBinaryValue(UInt128.ToByteArray(stringLength16Plus));
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.ArraysName);
                jsonWriter.WriteArrayStart();
                foreach (UInt128 array in this.arrays)
                {
                    jsonWriter.WriteBinaryValue(UInt128.ToByteArray(array));
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.ObjectName);
                jsonWriter.WriteArrayStart();
                foreach (UInt128 objectHash in this.objects)
                {
                    jsonWriter.WriteBinaryValue(UInt128.ToByteArray(objectHash));
                }
                jsonWriter.WriteArrayEnd();

                jsonWriter.WriteFieldName(UnorderdDistinctMap.SimpleValuesName);
                jsonWriter.WriteStringValue(this.simpleValues.ToString());

                jsonWriter.WriteObjectEnd();

                ReadOnlyMemory<byte> memory = jsonWriter.GetResult();
                if (!MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> buffer))
                {
                    buffer = new ArraySegment<byte>(memory.ToArray());
                }

                return Convert.ToBase64String(buffer.Array, buffer.Offset, buffer.Count);
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
                    // Zero out the array since you want all trailing bytes to be 0 for the conversions that happen next.
                    Array.Clear(this.utf8Buffer, 0, this.utf8Buffer.Length);
                    Encoding.UTF8.GetBytes(value, 0, value.Length, this.utf8Buffer, 0);

                    if (utf8Length == 0)
                    {
                        added = this.AddSimpleValue(SimpleValues.EmptyString);
                    }
                    else if (utf8Length <= UnorderdDistinctMap.UIntLength)
                    {
                        uint uintValue = BitConverter.ToUInt32(this.utf8Buffer, 0);
                        added = this.stringsLength4.Add(uintValue);
                    }
                    else if (utf8Length <= UnorderdDistinctMap.ULongLength)
                    {
                        ulong uLongValue = BitConverter.ToUInt64(this.utf8Buffer, 0);
                        added = this.stringsLength8.Add(uLongValue);
                    }
                    else
                    {
                        UInt128 uInt128Value = UInt128.FromByteArray(this.utf8Buffer);
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

            public static TryCatch<DistinctMap> TryCreate(string continuationToken)
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
                    byte[] binaryBuffer = Convert.FromBase64String(continuationToken);
                    CosmosElement cosmosElement = CosmosElement.CreateFromBuffer(binaryBuffer);
                    if (!(cosmosElement is CosmosObject hashDictionary))
                    {
                        return TryCatch<DistinctMap>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(UnorderdDistinctMap)} continuation token was malformed."));
                    }

                    // Numbers
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.NumbersName, out CosmosArray numbersArray))
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
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.StringsLength4Name, out CosmosArray stringsLength4Array))
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
                    if (!hashDictionary.TryGetValue(UnorderdDistinctMap.StringsLength8Name, out CosmosArray stringsLength8Array))
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
                    stringsLength16 = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.StringsLength16Name);

                    // Strings Length 24
                    stringsLength16Plus = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.StringsLength16PlusName);

                    // Array
                    arrays = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.ArraysName);

                    // Object
                    objects = Parse128BitHashes(hashDictionary, UnorderdDistinctMap.ObjectName);

                    // Simple Values
                    CosmosElement rawSimpleValues = hashDictionary[UnorderdDistinctMap.SimpleValuesName];
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
