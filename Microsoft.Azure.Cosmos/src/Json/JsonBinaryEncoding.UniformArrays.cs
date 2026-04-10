// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.CompilerServices;

    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Checks if the given <paramref name="typeMarker"/> represents a uniform array of numbers.
        /// </summary>
        /// <param name="typeMarker">The TypeMarker value to check.</param>
        /// <returns>
        /// Returns <code>true</code> if the specified value represents a uniform array of numbers;
        /// otherwise <code>false</code>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUniformArrayOfNumbersTypeMarker(byte typeMarker)
        {
            return (typeMarker == TypeMarker.ArrNumC1) || (typeMarker == TypeMarker.ArrNumC2);
        }

        /// <summary>
        /// Checks if the given <paramref name="typeMarker"/> represents a uniform array of number arrays.
        /// </summary>
        /// <param name="typeMarker">The TypeMarker value to check.</param>
        /// <returns>
        /// Returns <code>true</code> if the specified value represents a uniform array of number arrays;
        /// otherwise <code>false</code>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUniformArrayOfNumberArraysTypeMarker(byte typeMarker)
        {
            return (typeMarker == TypeMarker.ArrArrNumC1C1) || (typeMarker == TypeMarker.ArrArrNumC2C2);
        }

        /// <summary>
        /// Checks if the given <paramref name="typeMarker"/> represents a uniform array.
        /// </summary>
        /// <param name="typeMarker">The TypeMarker value to check.</param>
        /// <returns>
        /// Returns <code>true</code> if the specified value represents a uniform array;
        /// otherwise <code>false</code>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUniformArrayTypeMarker(byte typeMarker)
        {
            return IsUniformArrayOfNumbersTypeMarker(typeMarker) || IsUniformArrayOfNumberArraysTypeMarker(typeMarker);
        }

        public static int GetUniformArrayItemCount(ReadOnlySpan<byte> uniformArrayPrefix)
        {
            if (uniformArrayPrefix.IsEmpty) return default;

            byte arrayTypeMarker = uniformArrayPrefix[0];
            switch (arrayTypeMarker)
            {
                case TypeMarker.ArrNumC1:
                    // | Array TM | Number Item TM | Number Item Count |
                    return GetFixedSizedValue<byte>(uniformArrayPrefix.Slice(2));

                case TypeMarker.ArrNumC2:
                    // | Array TM | Number Item TM | Number Item Count |
                    return GetFixedSizedValue<ushort>(uniformArrayPrefix.Slice(2));

                case TypeMarker.ArrArrNumC1C1:
                    // | Array TM | Array Item TM | Number Item TM | Number Item Count | Array Item Count |
                    return GetFixedSizedValue<byte>(uniformArrayPrefix.Slice(4));

                case TypeMarker.ArrArrNumC2C2:
                    // | Array TM | Array Item TM | Number Item TM | Number Item Count | Array Item Count |
                    return GetFixedSizedValue<ushort>(uniformArrayPrefix.Slice(5));

                default:
                    throw new InvalidCastException($"Unexpected uniform array type marker: {arrayTypeMarker}.");
            }
        }

        public static int GetUniformArrayItemSize(ReadOnlySpan<byte> uniformArrayPrefix)
        {
            if (uniformArrayPrefix.IsEmpty) return default;

            byte arrayTypeMarker = uniformArrayPrefix[0];
            switch (arrayTypeMarker)
            {
                case TypeMarker.ArrNumC1:
                case TypeMarker.ArrNumC2:
                    // | Array TM | Number Item TM | Number Item Count |
                    return GetValueLength(uniformArrayPrefix.Slice(1));

                case TypeMarker.ArrArrNumC1C1:
                case TypeMarker.ArrArrNumC2C2:
                    {
                        // | Array TM | Array Item TM | Number Item TM | Number Item Count | Array Item Count |
                        int numberItemSize = GetValueLength(uniformArrayPrefix.Slice(2));
                        ushort numberItemCount = arrayTypeMarker == TypeMarker.ArrArrNumC1C1 ?
                            GetFixedSizedValue<byte>(uniformArrayPrefix.Slice(3)) :
                            GetFixedSizedValue<ushort>(uniformArrayPrefix.Slice(3));
                        return numberItemSize * numberItemCount;
                    }

                default:
                    throw new InvalidCastException($"Unexpected uniform array type marker: {arrayTypeMarker}.");
            }
        }

        public static UniformArrayInfo GetUniformArrayInfo(ReadOnlySpan<byte> arrayPrefix, bool isNested = false)
        {
            if (arrayPrefix.IsEmpty)
                throw new ArgumentException($"{nameof(arrayPrefix)} is not empty");

            byte arrayTypeMarker = arrayPrefix[0];
            switch (arrayTypeMarker)
            {
                case TypeMarker.ArrNumC1:
                    {
                        // | Array TM | Number Item TM | Number Item Count |
                        return new UniformArrayInfo(
                            itemTypeMarker: arrayPrefix[1],
                            prefixSize: isNested ? 0 : 3,
                            itemCount: GetFixedSizedValue<byte>(arrayPrefix.Slice(2)),
                            itemSize: ValueLengths.GetUniformNumberArrayItemSize(arrayPrefix[1]));
                    }

                case TypeMarker.ArrNumC2:
                    {
                        // | Array TM | Number Item TM | Number Item Count |
                        return new UniformArrayInfo(
                            itemTypeMarker: arrayPrefix[1],
                            prefixSize: isNested ? 0 : 4,
                            itemCount: GetFixedSizedValue<ushort>(arrayPrefix.Slice(2)),
                            itemSize: ValueLengths.GetUniformNumberArrayItemSize(arrayPrefix[1]));
                    }

                case TypeMarker.ArrArrNumC1C1:
                case TypeMarker.ArrArrNumC2C2:
                    {
                        // | Array TM | Array Item TM | Number Item TM | Number Item Count | Array Item Count |
                        UniformArrayInfo nestedArrayInfo = GetUniformArrayInfo(arrayPrefix.Slice(1), isNested: true);
                        return new UniformArrayInfo(
                            itemTypeMarker: arrayPrefix[1],
                            itemCount: arrayTypeMarker == TypeMarker.ArrArrNumC1C1 ?
                                GetFixedSizedValue<byte>(arrayPrefix.Slice(4)) :
                                GetFixedSizedValue<ushort>(arrayPrefix.Slice(5)),
                            itemSize: nestedArrayInfo.ItemCount * nestedArrayInfo.ItemSize,
                            prefixSize: arrayTypeMarker == TypeMarker.ArrArrNumC1C1 ? 5 : 7,
                            nestedArrayInfo: nestedArrayInfo);
                    }

                default:
                    return null;
            }
        }

        public static bool Equals(UniformArrayInfo arrayInfo1,  UniformArrayInfo arrayInfo2)
        {
            if (object.ReferenceEquals(arrayInfo1, arrayInfo2)) return true;
            if ((arrayInfo1 == null) || (arrayInfo2 == null)) return false;

            return (arrayInfo1.ItemTypeMarker == arrayInfo2.ItemTypeMarker) &&
                (arrayInfo1.ItemCount == arrayInfo2.ItemCount) &&
                (arrayInfo1.ItemSize == arrayInfo2.ItemSize) &&
                (arrayInfo1.PrefixSize == arrayInfo2.PrefixSize) &&
                Equals(arrayInfo1.NestedArrayInfo, arrayInfo2.NestedArrayInfo);
        }

        public class UniformArrayInfo
        {
            public byte ItemTypeMarker { get; }
            public int ItemCount { get; }
            public int ItemSize { get; }
            public int PrefixSize { get; }
            public UniformArrayInfo NestedArrayInfo { get; }

            public UniformArrayInfo(
                byte itemTypeMarker,
                int itemCount,
                int itemSize,
                int prefixSize = default,
                UniformArrayInfo nestedArrayInfo = default)
            {
                this.ItemTypeMarker = itemTypeMarker;
                this.ItemCount = itemCount;
                this.ItemSize = itemSize;
                this.PrefixSize = prefixSize;
                this.NestedArrayInfo = nestedArrayInfo;
            }
        }
    }
}
