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
                if (buffer.Length < 2)
                {
                    throw new ArgumentException($"{nameof(buffer)} must have at least two byte.");
                }

                if (buffer.Span[0] != (byte)JsonSerializationFormat.Binary)
                {
                    throw new ArgumentNullException("buffer must be binary encoded.");
                }

                // offset for the 0x80 (128) binary serialization type marker.
                buffer = buffer.Slice(1);

                // Only navigate the outer most json value and trim off trailing bytes
                int jsonValueLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                if (buffer.Length < jsonValueLength)
                {
                    throw new ArgumentException("buffer is shorter than the length prefix.");
                }

                buffer = buffer.Slice(0, jsonValueLength);

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
            public override IJsonNavigatorNode GetRootNode()
            {
                return new BinaryNavigatorNode(this.buffer);
            }

            /// <inheritdoc />
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (!(node is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(BinaryNavigatorNode)}");
                }

                ReadOnlyMemory<byte> buffer = binaryNavigatorNode.Buffer;

                if (buffer.Length == 0)
                {
                    throw new ArgumentException($"Node must not be empty.");
                }

                JsonNodeType jsonNodeType = JsonBinaryEncoding.GetNodeType(buffer.Span[0]);
                return jsonNodeType;
            }

            /// <inheritdoc />
            public override Number64 GetNumberValue(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Number,
                    numberNode);
                return JsonBinaryEncoding.GetNumberValue(buffer.Span);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                IJsonNavigatorNode stringNode,
                out ReadOnlyMemory<byte> bufferedStringValue)
            {
                //TODO (brchon): implement this when optimizing code.
                bufferedStringValue = null;
                return false;
            }

            /// <inheritdoc />
            public override string GetStringValue(IJsonNavigatorNode stringNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.String,
                    stringNode);
                return JsonBinaryEncoding.GetStringValue(buffer.Span, this.jsonStringDictionary);
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Int8,
                    numberNode);
                return JsonBinaryEncoding.GetInt8Value(buffer.Span);
            }

            /// <inheritdoc />
            public override short GetInt16Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Int16,
                    numberNode);
                return JsonBinaryEncoding.GetInt16Value(buffer.Span);
            }

            /// <inheritdoc />
            public override int GetInt32Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Int32,
                    numberNode);
                return JsonBinaryEncoding.GetInt32Value(buffer.Span);
            }

            /// <inheritdoc />
            public override long GetInt64Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Int64,
                    numberNode);
                return JsonBinaryEncoding.GetInt64Value(buffer.Span);
            }

            /// <inheritdoc />
            public override float GetFloat32Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Float32,
                    numberNode);
                return JsonBinaryEncoding.GetFloat32Value(buffer.Span);
            }

            /// <inheritdoc />
            public override double GetFloat64Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Float64,
                    numberNode);
                return JsonBinaryEncoding.GetFloat64Value(buffer.Span);
            }

            /// <inheritdoc />
            public override uint GetUInt32Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.UInt32,
                    numberNode);
                return JsonBinaryEncoding.GetUInt32Value(buffer.Span);
            }

            /// <inheritdoc />
            public override Guid GetGuidValue(IJsonNavigatorNode guidNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Guid,
                    guidNode);
                return JsonBinaryEncoding.GetGuidValue(buffer.Span);
            }

            /// <inheritdoc />
            public override ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode)
            {
                if (!this.TryGetBufferedBinaryValue(
                    binaryNode,
                    out ReadOnlyMemory<byte> bufferedBinaryValue))
                {
                    throw new JsonInvalidTokenException();
                }

                return bufferedBinaryValue;
            }

            public override bool TryGetBufferedBinaryValue(
                IJsonNavigatorNode binaryNode,
                out ReadOnlyMemory<byte> bufferedBinaryValue)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Binary,
                    binaryNode);
                bufferedBinaryValue = JsonBinaryEncoding.GetBinaryValue(buffer);
                return true;
            }

            /// <inheritdoc />
            public override int GetArrayItemCount(IJsonNavigatorNode arrayNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Array,
                    arrayNode);
                byte typeMarker = buffer.Span[0];
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
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength).Span);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        count = MemoryMarshal.Read<ushort>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength).Span);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        count = MemoryMarshal.Read<uint>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength).Span);
                        break;

                    // Arrays with length prefix
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(firstValueOffset).Span);
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
            public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Array,
                    arrayNode);

                if (index < 0)
                {
                    throw new IndexOutOfRangeException();
                }

                // TODO (brchon): We can optimize for the case where the count is serialized so we can avoid using the linear time call to TryGetValueAt().
                if (!JsonBinaryNavigator.TryGetValueAt(buffer, index, out ReadOnlyMemory<byte> arrayItem))
                {
                    throw new IndexOutOfRangeException($"Tried to access index:{index} in an array.");
                }

                return new BinaryNavigatorNode(arrayItem);
            }

            /// <inheritdoc />
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Array,
                    arrayNode);

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
                    if (arrayItemLength > buffer.Length)
                    {
                        // Array Item got cut off.
                        throw new JsonInvalidTokenException();
                    }

                    // Create a buffer for that array item
                    IJsonNavigatorNode arrayItem = new BinaryNavigatorNode(buffer.Slice(0, arrayItemLength));
                    yield return arrayItem;

                    // Slice off the array item
                    buffer = buffer.Slice(arrayItemLength);
                }
            }

            /// <inheritdoc />
            public override int GetObjectPropertyCount(IJsonNavigatorNode objectNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Object,
                    objectNode);

                byte typeMarker = buffer.Span[0];
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
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength).Span);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        count = MemoryMarshal.Read<ushort>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength).Span);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        count = MemoryMarshal.Read<uint>(buffer
                            .Slice(JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength).Span);
                        break;

                    // Object with length prefix
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(firstObjectPropertyOffset).Span);
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
            public override bool TryGetObjectProperty(
                IJsonNavigatorNode objectNode,
                string propertyName,
                out ObjectProperty objectProperty)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Object,
                    objectNode);

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
            public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Object,
                    objectNode);

                byte typeMarker = buffer.Span[0];
                int firstValueOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);

                buffer = buffer.Slice(firstValueOffset);
                while (buffer.Length != 0)
                {
                    int nameNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (nameNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }
                    ReadOnlyMemory<byte> nameNode = buffer.Slice(0, nameNodeLength);
                    buffer = buffer.Slice(nameNodeLength);

                    int valueNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (valueNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }
                    ReadOnlyMemory<byte> valueNode = buffer.Slice(0, valueNodeLength);
                    buffer = buffer.Slice(valueNodeLength);

                    yield return new ObjectProperty(
                        new BinaryNavigatorNode(nameNode),
                        new BinaryNavigatorNode(valueNode));
                }
            }

            /// <inheritdoc />
            public override bool TryGetBufferedRawJson(
                IJsonNavigatorNode jsonNode,
                out ReadOnlyMemory<byte> bufferedRawJson)
            {
                if (jsonNode == null)
                {
                    throw new ArgumentNullException(nameof(jsonNode));
                }

                if (!(jsonNode is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(jsonNode)} must be a {nameof(BinaryNavigatorNode)}");
                }

                ReadOnlyMemory<byte> buffer = binaryNavigatorNode.Buffer;

                if (buffer.Length == 0)
                {
                    throw new ArgumentException($"Node must not be empty.");
                }

                bufferedRawJson = buffer;
                return true;
            }

            private static int GetValueCount(ReadOnlySpan<byte> node)
            {
                int count = 0;
                while (!node.IsEmpty)
                {
                    count++;
                    int nodeLength = JsonBinaryEncoding.GetValueLength(node);
                    node = node.Slice(nodeLength);
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

            private static ReadOnlyMemory<byte> GetNodeOfType(
                JsonNodeType expected,
                IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (!(node is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(BinaryNavigatorNode)}");
                }

                ReadOnlyMemory<byte> buffer = binaryNavigatorNode.Buffer;

                if (buffer.Length == 0)
                {
                    throw new ArgumentException($"Node must not be empty.");
                }

                JsonNodeType actual = JsonBinaryEncoding.GetNodeType(buffer.Span[0]);
                if (actual != expected)
                {
                    throw new ArgumentException($"Node needs to be of type {expected}.");
                }

                return buffer;
            }

            private sealed class BinaryNavigatorNode : IJsonNavigatorNode
            {
                public BinaryNavigatorNode(ReadOnlyMemory<byte> buffer)
                {
                    this.Buffer = buffer;
                }

                public ReadOnlyMemory<byte> Buffer { get; }
            }
        }
    }
}
