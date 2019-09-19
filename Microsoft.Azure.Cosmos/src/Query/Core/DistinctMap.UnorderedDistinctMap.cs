//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using UInt128 = Documents.UInt128;

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
            /// Length of UInt192 (in bytes).
            /// </summary>
            private const int UInt192Length = 24;

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

            /// <summary>
            /// Buffer that gets reused to convert a .net string (utf-16) to a (utf-8) byte array.
            /// </summary>
            private readonly byte[] utf8Buffer = new byte[UnorderdDistinctMap.UInt192Length];

            /// <summary>
            /// HashSet for all numbers seen.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<double> numbers = new HashSet<double>();

            /// <summary>
            /// HashSet for all strings seen of length less than or equal to 4 stored as a uint.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<uint> stringsLength4 = new HashSet<uint>();

            /// <summary>
            /// HashSet for all strings seen of length less than or equal to 8 stored as a ulong.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<ulong> stringLength8 = new HashSet<ulong>();

            /// <summary>
            /// HashSet for all strings of length less than or equal to 16 stored as a UInt128.
            /// This takes less space than a 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<UInt128> stringLength16 = new HashSet<UInt128>();

            /// <summary>
            /// HashSet for all strings seen of length less than or equal to 24 stored as a UInt192.
            /// This takes the same space as 24 byte hash and has full fidelity.
            /// </summary>
            private readonly HashSet<UInt192> stringLength24 = new HashSet<UInt192>();

            /// <summary>
            /// HashSet for all strings seen of length greater than 24 stored as a UInt192.
            /// This set only stores the hash, since we don't want to spend the space for storing large strings.
            /// </summary>
            private readonly HashSet<UInt192> stringLength24Plus = new HashSet<UInt192>();

            /// <summary>
            /// HashSet for all arrays seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large arrays.
            /// </summary>
            private readonly HashSet<UInt192> arrays = new HashSet<UInt192>();

            /// <summary>
            /// HashSet for all object seen.
            /// This set only stores the hash, since we don't want to spend the space for storing large objects.
            /// </summary>
            private readonly HashSet<UInt192> objects = new HashSet<UInt192>();

            /// <summary>
            /// Stores all the simple values that we don't want to dedicate a hash set for.
            /// </summary>
            private SimpleValues simpleValues;

            /// <summary>
            /// Adds a JToken to this map if it hasn't already been added.
            /// </summary>
            /// <param name="cosmosElement">The element to add.</param>
            /// <param name="hash">The hash of the token.</param>
            /// <returns>Whether or not the item was added to this Distinct Map.</returns>
            public override bool Add(CosmosElement cosmosElement, out UInt192? hash)
            {
                // Unordered distinct does not need to return a valid hash.
                // Since it doesn't need the last hash for a continuation.
                hash = null;
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
                        double number;
                        if (cosmosNumber.IsFloatingPoint)
                        {
                            number = cosmosNumber.AsFloatingPoint().Value;
                        }
                        else
                        {
                            number = cosmosNumber.AsInteger().Value;
                        }

                        added = this.AddNumberValue(number);
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

            /// <summary>
            /// Adds a number value to the map.
            /// </summary>
            /// <param name="value">The value to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddNumberValue(double value)
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

                // If you fit the string with full fidelity in 24 bytes, then you might as well just hash the string.
                if (utf8Length <= UnorderdDistinctMap.UInt192Length)
                {
                    // Zero out the array since you want all trailing bytes to be 0 for the conversions that happen next.
                    Array.Clear(this.utf8Buffer, 0, this.utf8Buffer.Length);
                    Encoding.UTF8.GetBytes(value, 0, utf8Length, this.utf8Buffer, 0);

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
                        added = this.stringLength8.Add(uLongValue);
                    }
                    else if (utf8Length <= UnorderdDistinctMap.UInt128Length)
                    {
                        UInt128 uInt128Value = UInt128.FromByteArray(this.utf8Buffer, 0);
                        added = this.stringLength16.Add(uInt128Value);
                    }
                    else
                    {
                        UInt192 uInt192Value = UInt192.FromByteArray(this.utf8Buffer, 0);
                        added = this.stringLength24.Add(uInt192Value);
                    }
                }
                else
                {
                    // Else the string is too large and we will just store the hash.
                    UInt192 uint192Value = DistinctMap.GetHash(CosmosString.Create(value));
                    added = this.stringLength24Plus.Add(uint192Value);
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
                UInt192 hash = DistinctMap.GetHash(array);
                return this.arrays.Add(hash);
            }

            /// <summary>
            /// Adds an object value to the distinct map.
            /// </summary>
            /// <param name="cosmosObject">The object to add.</param>
            /// <returns>Whether or not the value was successfully added.</returns>
            private bool AddObjectValue(CosmosObject cosmosObject)
            {
                UInt192 hash = DistinctMap.GetHash(cosmosObject);
                return this.objects.Add(hash);
            }
        }
    }
}
