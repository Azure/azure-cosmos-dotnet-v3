// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.InteropServices;

    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Gets the number value from the binary reader.
        /// </summary>
        /// <param name="numberToken">The buffer to read the number from.</param>
        /// <param name="uniformArrayInfo">The value’s external array info, if it occurs within a uniform number array.</param>
        /// <returns>The number value from the binary reader.</returns>
        public static Number64 GetNumberValue(ReadOnlySpan<byte> numberToken, UniformArrayInfo uniformArrayInfo)
        {
            if (!JsonBinaryEncoding.TryGetNumberValue(numberToken, uniformArrayInfo, out Number64 number64, out int _))
            {
                throw new JsonNotNumberTokenException();
            }

            return number64;
        }

        /// <summary>
        /// Try Get NumberValue
        /// </summary>
        /// <param name="numberToken">The buffer.</param>
        /// <param name="uniformArrayInfo">The value’s external array info, if it occurs within a uniform number array.</param>
        /// <param name="number64">The number.</param>
        /// <param name="bytesConsumed">The number of bytes consumed</param>
        /// <returns>Whether a number was parsed.</returns>
        public static bool TryGetNumberValue(ReadOnlySpan<byte> numberToken, UniformArrayInfo uniformArrayInfo, out Number64 number64, out int bytesConsumed)
        {
            number64 = 0;
            bytesConsumed = 0;

            if (numberToken.IsEmpty)
            {
                return false;
            }

            if (uniformArrayInfo != null)
            {
                switch (uniformArrayInfo.ItemTypeMarker)
                {
                    case TypeMarker.Int8:
                        number64 = MemoryMarshal.Read<sbyte>(numberToken);
                        bytesConsumed = sizeof(sbyte);
                        break;

                    case TypeMarker.UInt8:
                        number64 = MemoryMarshal.Read<byte>(numberToken);
                        bytesConsumed = sizeof(byte);
                        break;

                    case TypeMarker.Int16:
                        if (numberToken.Length < sizeof(short))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<short>(numberToken);
                        bytesConsumed = sizeof(short);
                        break;

                    case TypeMarker.Int32:
                        if (numberToken.Length < sizeof(int))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<int>(numberToken);
                        bytesConsumed = sizeof(int);
                        break;

                    case TypeMarker.Int64:
                        if (numberToken.Length < sizeof(long))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<long>(numberToken);
                        bytesConsumed = sizeof(long);
                        break;

                    case TypeMarker.Float16:
                        // Currently not supported
                        return false;

                    case TypeMarker.Float32:
                        if (numberToken.Length < sizeof(float))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<float>(numberToken);
                        bytesConsumed = sizeof(float);
                        break;

                    case TypeMarker.Float64:
                        if (numberToken.Length < sizeof(double))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<double>(numberToken);
                        bytesConsumed = sizeof(double);
                        break;

                    default:
                        throw new JsonUnexpectedTokenException();
                }
            }
            else
            {
                byte typeMarker = numberToken[0];

                if (JsonBinaryEncoding.TypeMarker.IsEncodedNumberLiteral(typeMarker))
                {
                    number64 = typeMarker - JsonBinaryEncoding.TypeMarker.LiteralIntMin;
                    bytesConsumed = 1;
                }
                else
                {
                    switch (typeMarker)
                    {
                        case JsonBinaryEncoding.TypeMarker.NumberUInt8:
                            if (numberToken.Length < (1 + 1))
                            {
                                return false;
                            }

                            number64 = MemoryMarshal.Read<byte>(numberToken.Slice(1));
                            bytesConsumed = 1 + 1;
                            break;

                        case JsonBinaryEncoding.TypeMarker.NumberInt16:
                            if (numberToken.Length < (1 + 2))
                            {
                                return false;
                            }

                            number64 = MemoryMarshal.Read<short>(numberToken.Slice(1));
                            bytesConsumed = 1 + 2;
                            break;

                        case JsonBinaryEncoding.TypeMarker.NumberInt32:
                            if (numberToken.Length < (1 + 4))
                            {
                                return false;
                            }

                            number64 = MemoryMarshal.Read<int>(numberToken.Slice(1));
                            bytesConsumed = 1 + 4;
                            break;

                        case JsonBinaryEncoding.TypeMarker.NumberInt64:
                            if (numberToken.Length < (1 + 8))
                            {
                                return false;
                            }

                            number64 = MemoryMarshal.Read<long>(numberToken.Slice(1));
                            bytesConsumed = 1 + 8;
                            break;

                        case JsonBinaryEncoding.TypeMarker.NumberDouble:
                            if (numberToken.Length < (1 + 8))
                            {
                                return false;
                            }

                            number64 = MemoryMarshal.Read<double>(numberToken.Slice(1));
                            bytesConsumed = 1 + 8;
                            break;

                        default:
                            throw new JsonInvalidNumberException();
                    }
                }
            }

            return true;
        }

        public static sbyte GetInt8Value(ReadOnlySpan<byte> int8Token)
        {
            if (!JsonBinaryEncoding.TryGetInt8Value(int8Token, out sbyte int8Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int8Value;
        }

        public static bool TryGetInt8Value(
            ReadOnlySpan<byte> int8Token,
            out sbyte int8Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<sbyte>(
                int8Token,
                JsonBinaryEncoding.TypeMarker.Int8,
                out int8Value);
        }

        public static short GetInt16Value(ReadOnlySpan<byte> int16Token)
        {
            if (!JsonBinaryEncoding.TryGetInt16Value(int16Token, out short int16Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int16Value;
        }

        public static bool TryGetInt16Value(
            ReadOnlySpan<byte> int16Token,
            out short int16Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<short>(
                int16Token,
                JsonBinaryEncoding.TypeMarker.Int16,
                out int16Value);
        }

        public static int GetInt32Value(ReadOnlySpan<byte> int32Token)
        {
            if (!JsonBinaryEncoding.TryGetInt32Value(int32Token, out int int32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int32Value;
        }

        public static bool TryGetInt32Value(
            ReadOnlySpan<byte> int32Token,
            out int int32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<int>(
                int32Token,
                JsonBinaryEncoding.TypeMarker.Int32,
                out int32Value);
        }

        public static long GetInt64Value(ReadOnlySpan<byte> int64Token)
        {
            if (!JsonBinaryEncoding.TryGetInt64Value(int64Token, out long int64Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int64Value;
        }

        public static bool TryGetInt64Value(
            ReadOnlySpan<byte> int64Token,
            out long int64Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<long>(
                int64Token,
                JsonBinaryEncoding.TypeMarker.Int64,
                out int64Value);
        }

        public static uint GetUInt32Value(ReadOnlySpan<byte> uInt32Token)
        {
            if (!JsonBinaryEncoding.TryGetUInt32Value(uInt32Token, out uint uInt32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return uInt32Value;
        }

        public static bool TryGetUInt32Value(
            ReadOnlySpan<byte> uInt32Token,
            out uint uInt32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<uint>(
                uInt32Token,
                JsonBinaryEncoding.TypeMarker.UInt32,
                out uInt32Value);
        }

        public static float GetFloat32Value(ReadOnlySpan<byte> float32Token)
        {
            if (!JsonBinaryEncoding.TryGetFloat32Value(float32Token, out float float32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return float32Value;
        }

        public static bool TryGetFloat32Value(
            ReadOnlySpan<byte> float32Token,
            out float float32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<float>(
                float32Token,
                JsonBinaryEncoding.TypeMarker.Float32,
                out float32Value);
        }

        public static double GetFloat64Value(ReadOnlySpan<byte> float64Token)
        {
            if (!JsonBinaryEncoding.TryGetFloat64Value(float64Token, out double float64Value))
            {
                throw new JsonInvalidNumberException();
            }

            return float64Value;
        }

        public static bool TryGetFloat64Value(
            ReadOnlySpan<byte> float64Token,
            out double float64Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<double>(
                float64Token,
                JsonBinaryEncoding.TypeMarker.Float64,
                out float64Value);
        }
    }
}