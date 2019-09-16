//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Partial class that wraps the private JsonTextNavigator
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonNavigator : IJsonNavigator
    {
        /// <summary>
        /// JsonNavigator that know how to navigate JSONs in binary serialization.
        /// </summary>
        private sealed class JsonBinaryNavigator : JsonNavigator
        {
            private readonly ReadOnlyMemory<byte> buffer;
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryNavigator class
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
            /// <param name="skipValidation">whether to skip validation or not.</param>
            public JsonBinaryNavigator(
                ReadOnlyMemory<byte> buffer,
                JsonStringDictionary jsonStringDictionary,
                bool skipValidation = false)
            {
                if (buffer.Length < 1)
                {
                    throw new ArgumentException($"{nameof(buffer)} must have at least one byte.");
                }

                this.buffer = buffer;
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Binary;
                }
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetRootNode()
            {
                // offset for the 0x80 (128) binary serialization type marker.
                return this.buffer.Slice(1);
            }

            /// <inheritdoc />
            public override JsonNodeType GetNodeType(ReadOnlyMemory<byte> node)
            {
                if (node.Length == 0)
                {
                    throw new ArgumentException($"{nameof(node)} must not be empty.");
                }

                return JsonBinaryEncoding.GetNodeType(node.Span[0]);
            }

            /// <inheritdoc />
            public override Number64 GetNumberValue(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Number, numberNode.Span);
                return JsonBinaryEncoding.GetNumberValue(numberNode.Span);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                ReadOnlyMemory<byte> stringNode,
                out ReadOnlyMemory<byte> bufferedStringValue)
            {
                //TODO (brchon): implement this when optimizing code.
                bufferedStringValue = null;
                return false;
            }

            /// <inheritdoc />
            public override string GetStringValue(ReadOnlyMemory<byte> stringNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.String, stringNode.Span);
                return JsonBinaryEncoding.GetStringValue(stringNode.Span, this.jsonStringDictionary);
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Int8, numberNode.Span);
                return JsonBinaryEncoding.GetInt8Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override short GetInt16Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Int16, numberNode.Span);
                return JsonBinaryEncoding.GetInt16Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override int GetInt32Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Int32, numberNode.Span);
                return JsonBinaryEncoding.GetInt32Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override long GetInt64Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Int64, numberNode.Span);
                return JsonBinaryEncoding.GetInt64Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override float GetFloat32Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Float32, numberNode.Span);
                return JsonBinaryEncoding.GetFloat32Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override double GetFloat64Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Float64, numberNode.Span);
                return JsonBinaryEncoding.GetFloat64Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override uint GetUInt32Value(ReadOnlyMemory<byte> numberNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.UInt32, numberNode.Span);
                return JsonBinaryEncoding.GetUInt32Value(numberNode.Span);
            }

            /// <inheritdoc />
            public override Guid GetGuidValue(ReadOnlyMemory<byte> guidNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Guid, guidNode.Span);
                return JsonBinaryEncoding.GetGuidValue(guidNode.Span);
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue(ReadOnlyMemory<byte> binaryNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Binary, binaryNode.Span);
                return JsonBinaryEncoding.GetBinaryValue(binaryNode);
            }

            /// <inheritdoc />
            public override int GetArrayItemCount(ReadOnlyMemory<byte> arrayNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Array, arrayNode.Span);
                ReadOnlySpan<byte> buffer = arrayNode.Span;
                byte typeMarker = arrayNode.Span[0];
                int firstValueOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                long count;
                switch (typeMarker)
                {
                    // Empty and Single Array
                    case JsonBinaryEncoding.TypeMarker.EmptyArray:
                        count = 0;
                        break;
                    case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                        count = 1;
                        break;

                    // Arrays with length and count prefix
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                        count = MemoryMarshal.Read<byte>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        count = MemoryMarshal.Read<ushort>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        count = MemoryMarshal.Read<uint>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength));
                        break;

                    // Arrays with length prefix
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        byte array1ByteLength = MemoryMarshal.Read<byte>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength));
                        count = JsonBinaryNavigator.GetValueCount(buffer
                            .Slice(0, array1ByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        ushort array2ByteLength = MemoryMarshal.Read<ushort>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength));
                        count = JsonBinaryNavigator.GetValueCount(buffer
                            .Slice(0, array2ByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        uint array4ByteLength = MemoryMarshal.Read<uint>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength));
                        count = JsonBinaryNavigator.GetValueCount(buffer
                            .Slice(0, (int)array4ByteLength));
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected array type marker: {typeMarker}");
                }

                if (count > int.MaxValue)
                {
                    throw new InvalidOperationException("count can not be more than int.MaxValue");
                }

                return (int)count;
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetArrayItemAt(ReadOnlyMemory<byte> arrayNode, int index)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Array, arrayNode.Span);

                if (index < 0)
                {
                    throw new IndexOutOfRangeException();
                }

                // TODO (brchon): We can optimize for the case where the count is serialized so we can avoid using the linear time call to TryGetValueAt().
                if (!JsonBinaryNavigator.TryGetValueAt(arrayNode, index, out ReadOnlyMemory<byte> arrayItem))
                {
                    throw new IndexOutOfRangeException($"Tried to access index:{index} in an array.");
                }

                return arrayItem;
            }

            /// <inheritdoc />
            public override IEnumerable<ReadOnlyMemory<byte>> GetArrayItems(ReadOnlyMemory<byte> arrayNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Array, arrayNode.Span);

                ReadOnlyMemory<byte> buffer = arrayNode;
                byte typeMarker = buffer.Span[0];

                int firstArrayItemOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                int arrayLength = JsonBinaryEncoding.GetValueLength(buffer.Span);

                // Scope to just the array
                buffer = buffer.Slice(0, (int)arrayLength);

                // Seek to the first array item
                buffer = buffer.Slice(firstArrayItemOffset);

                while (buffer.Length != 0)
                {
                    int arrayItemLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (arrayItemLength >= buffer.Length)
                    {
                        // Array Item got cut off.
                        throw new JsonInvalidTokenException();
                    }

                    // Create a buffer for that array item
                    ReadOnlyMemory<byte> arrayItem = buffer.Slice(0, arrayItemLength);
                    yield return arrayItem;

                    // Slice off the array item
                    buffer.Slice(arrayItemLength);
                }
            }

            /// <inheritdoc />
            public override int GetObjectPropertyCount(ReadOnlyMemory<byte> objectNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Object, objectNode.Span);

                ReadOnlySpan<byte> buffer = objectNode.Span;

                byte typeMarker = buffer[0];
                int firstObjectPropertyOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                long count;
                switch (typeMarker)
                {
                    // Empty and Single Object
                    case JsonBinaryEncoding.TypeMarker.EmptyObject:
                        count = 0;
                        break;
                    case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                        // This number gets divided by 2 later.
                        count = 2;
                        break;

                    // Object with length and count prefix
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                        count = MemoryMarshal.Read<byte>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        count = MemoryMarshal.Read<ushort>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        count = MemoryMarshal.Read<uint>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength));
                        break;

                    // Object with length prefix
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        count = JsonBinaryNavigator.GetValueCount(buffer);
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected object type marker: {typeMarker}");
                }

                // Divide by 2 since the count includes fieldname and value as seperate entities
                count = count / 2;
                if (count > int.MaxValue)
                {
                    throw new InvalidOperationException("count can not be more than int.MaxValue");
                }

                return (int)count;
            }

            /// <inheritdoc />
            public override bool TryGetObjectProperty(ReadOnlyMemory<byte> objectNode, string propertyName, out ObjectProperty objectProperty)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Object, objectNode.Span);

                foreach (ObjectProperty objectPropertyNode in this.GetObjectProperties(objectNode))
                {
                    if (this.GetStringValue(objectPropertyNode.NameNode) == propertyName)
                    {
                        objectProperty = objectPropertyNode;
                        return true;
                    }
                }

                objectProperty = default(ObjectProperty);
                return false;
            }

            /// <inheritdoc />
            public override IEnumerable<ObjectProperty> GetObjectProperties(ReadOnlyMemory<byte> objectNode)
            {
                JsonBinaryNavigator.ThrowIfNotValidType(JsonNodeType.Object, objectNode.Span);

                byte typeMarker = objectNode.Span[0];
                int firstValueOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);

                objectNode = objectNode.Slice(firstValueOffset);
                while (objectNode.Length != 0)
                {
                    int nameNodeLength = JsonBinaryEncoding.GetValueLength(objectNode.Span);
                    ReadOnlyMemory<byte> nameNode = objectNode.Slice(0, nameNodeLength);
                    objectNode = objectNode.Slice(nameNodeLength);

                    int valueNodeLength = JsonBinaryEncoding.GetValueLength(objectNode.Span);
                    ReadOnlyMemory<byte> valueNode = objectNode.Slice(0, valueNodeLength);
                    objectNode = objectNode.Slice(valueNodeLength);

                    yield return new ObjectProperty(nameNode, valueNode);
                }
            }

            /// <inheritdoc />
            public override bool TryGetBufferedRawJson(
                ReadOnlyMemory<byte> jsonNode,
                out ReadOnlyMemory<byte> bufferedRawJson)
            {
                if (jsonNode.Length == 0)
                {
                    bufferedRawJson = default(ReadOnlyMemory<byte>);
                    return false;
                }

                int nodeLength = JsonBinaryEncoding.GetValueLength(jsonNode.Span);
                bufferedRawJson = jsonNode.Slice(0, nodeLength);

                return true;
            }

            private static int GetValueCount(ReadOnlySpan<byte> node)
            {
                long bytesProcessed = 0;
                int count = 0;
                while (bytesProcessed < node.Length)
                {
                    count++;
                    bytesProcessed += JsonBinaryEncoding.GetValueLength(node);
                }

                return count;
            }

            private static bool TryGetValueAt(
                ReadOnlyMemory<byte> arrayNode,
                long index,
                out ReadOnlyMemory<byte> arrayItem)
            {
                ReadOnlyMemory<byte> buffer = arrayNode;
                byte typeMarker = buffer.Span[0];

                int firstArrayItemOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                int arrayLength = JsonBinaryEncoding.GetValueLength(buffer.Span);

                // Scope to just the array
                buffer = buffer.Slice(0, (int)arrayLength);

                // Seek to the first array item
                buffer = buffer.Slice(firstArrayItemOffset);

                for (long count = 0; count < index; count++)
                {
                    // Skip over the array item.
                    int arrayItemLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (arrayItemLength >= buffer.Length)
                    {
                        arrayItem = default;
                        return false;
                    }

                    buffer = buffer.Slice(arrayItemLength);
                }

                // Scope to just that array item
                int itemLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                buffer = buffer.Slice(0, itemLength);

                arrayItem = buffer;
                return true;
            }

            private static void ThrowIfNotValidType(JsonNodeType expected, ReadOnlySpan<byte> buffer)
            {
                if (buffer.Length == 0)
                {
                    throw new ArgumentException($"Node must not be empty.");
                }

                JsonNodeType actual = JsonBinaryEncoding.GetNodeType(buffer[0]);
                if (actual != expected)
                {
                    throw new ArgumentException($"Node needs to be of type {expected}.");
                }

            }
        }
    }
}
