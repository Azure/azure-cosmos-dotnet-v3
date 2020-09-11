//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Json.Interop;

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
            private readonly IReadOnlyJsonStringDictionary jsonStringDictionary;
            private readonly BinaryNavigatorNode rootNode;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryNavigator class
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
            public JsonBinaryNavigator(
                ReadOnlyMemory<byte> buffer,
                IReadOnlyJsonStringDictionary jsonStringDictionary)
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
                this.rootNode = new BinaryNavigatorNode(this.buffer, JsonBinaryEncoding.NodeTypes.GetNodeType(this.buffer.Span[0]));
            }

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

            /// <inheritdoc />
            public override IJsonNavigatorNode GetRootNode() => this.rootNode;

            /// <inheritdoc />
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (!(node is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(BinaryNavigatorNode)}");
                }

                return binaryNavigatorNode.JsonNodeType;
            }

            /// <inheritdoc />
            public override Number64 GetNumber64Value(IJsonNavigatorNode numberNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Number64,
                    numberNode);
                return JsonBinaryEncoding.GetNumberValue(buffer.Span);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                IJsonNavigatorNode stringNode,
                out Utf8Memory value)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.String,
                    stringNode);

                return JsonBinaryEncoding.TryGetBufferedStringValue(
                    Utf8Memory.UnsafeCreateNoValidation(buffer),
                    this.jsonStringDictionary,
                    out value);
            }

            /// <inheritdoc />
            public override string GetStringValue(IJsonNavigatorNode stringNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.String,
                    stringNode);
                return JsonBinaryEncoding.GetStringValue(
                    Utf8Memory.UnsafeCreateNoValidation(buffer),
                    this.jsonStringDictionary);
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

                return new BinaryNavigatorNode(
                    arrayItem,
                    JsonBinaryEncoding.NodeTypes.GetNodeType(arrayItem.Span[0]));
            }

            /// <inheritdoc />
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Array,
                    arrayNode);

                return this.GetArrayItemsInternal(buffer).Select((node) => (IJsonNavigatorNode)node);
            }

            private IEnumerable<BinaryNavigatorNode> GetArrayItemsInternal(ReadOnlyMemory<byte> buffer) => JsonBinaryEncoding.Enumerator
                .GetArrayItems(buffer)
                .Select(arrayItem => new BinaryNavigatorNode(arrayItem, JsonBinaryEncoding.NodeTypes.GetNodeType(arrayItem.Span[0])));

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
                count /= 2;
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
                _ = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Object,
                    objectNode);

                Utf8Span utf8StringPropertyName = Utf8Span.TranscodeUtf16(propertyName);
                foreach (ObjectProperty objectPropertyNode in this.GetObjectProperties(objectNode))
                {
                    if (this.TryGetBufferedStringValue(objectPropertyNode.NameNode, out Utf8Memory bufferedUtf8StringValue))
                    {
                        // First try and see if we can avoid materializing the UTF16 string.
                        if (utf8StringPropertyName.Equals(bufferedUtf8StringValue.Span))
                        {
                            objectProperty = objectPropertyNode;
                            return true;
                        }
                    }
                    else
                    {
                        if (this.GetStringValue(objectPropertyNode.NameNode) == propertyName)
                        {
                            objectProperty = objectPropertyNode;
                            return true;
                        }
                    }
                }

                objectProperty = default;
                return false;
            }

            /// <inheritdoc />
            public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode)
            {
                ReadOnlyMemory<byte> buffer = JsonBinaryNavigator.GetNodeOfType(
                    JsonNodeType.Object,
                    objectNode);

                return this.GetObjectPropertiesInternal(buffer)
                    .Select((objectPropertyInternal) => new ObjectProperty(
                        objectPropertyInternal.NameNode,
                        objectPropertyInternal.ValueNode));
            }

            private IEnumerable<ObjectPropertyInternal> GetObjectPropertiesInternal(ReadOnlyMemory<byte> buffer) => JsonBinaryEncoding.Enumerator
                .GetObjectProperties(buffer)
                .Select(property => new ObjectPropertyInternal(
                    new BinaryNavigatorNode(property.Name, JsonNodeType.FieldName),
                    new BinaryNavigatorNode(property.Value, JsonBinaryEncoding.NodeTypes.GetNodeType(property.Value.Span[0]))));

            public override IJsonReader CreateReader(IJsonNavigatorNode jsonNavigatorNode)
            {
                if (!(jsonNavigatorNode is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(jsonNavigatorNode)} must be a {nameof(BinaryNavigatorNode)}");
                }

                ReadOnlyMemory<byte> buffer = binaryNavigatorNode.Buffer;
                return JsonReader.Create(JsonSerializationFormat.Binary, buffer, this.jsonStringDictionary);
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

                if (buffer.IsEmpty)
                {
                    throw new ArgumentException($"Node must not be empty.");
                }

                JsonNodeType actual = JsonBinaryEncoding.NodeTypes.GetNodeType(buffer.Span[0]);
                if (actual != expected)
                {
                    throw new ArgumentException($"Node needs to be of type {expected}.");
                }

                return buffer;
            }

            public override void WriteNode(IJsonNavigatorNode jsonNavigatorNode, IJsonWriter jsonWriter)
            {
                if (!(jsonNavigatorNode is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentOutOfRangeException($"Expected {nameof(jsonNavigatorNode)} to be a {nameof(BinaryNavigatorNode)}.");
                }

                bool sameEncoding = this.SerializationFormat == jsonWriter.SerializationFormat;
                if (sameEncoding)
                {
                    bool isFieldName = binaryNavigatorNode.JsonNodeType == JsonNodeType.FieldName;
                    if (!(jsonWriter is IJsonBinaryWriterExtensions jsonBinaryWriter))
                    {
                        throw new InvalidOperationException($"Expected writer to implement: {nameof(IJsonBinaryWriterExtensions)}.");
                    }

                    jsonBinaryWriter.WriteRawJsonValue(
                        binaryNavigatorNode.Buffer,
                        isFieldName,
                        isRootNode: object.ReferenceEquals(jsonNavigatorNode, this.rootNode),
                        this.jsonStringDictionary);
                }
                else
                {
                    this.WriteToInternal(binaryNavigatorNode, jsonWriter);
                }
            }

            private void WriteToInternal(BinaryNavigatorNode binaryNavigatorNode, IJsonWriter jsonWriter)
            {
                ReadOnlyMemory<byte> buffer = binaryNavigatorNode.Buffer;
                JsonNodeType nodeType = binaryNavigatorNode.JsonNodeType;

                switch (nodeType)
                {
                    case JsonNodeType.Null:
                        jsonWriter.WriteNullValue();
                        break;

                    case JsonNodeType.False:
                        jsonWriter.WriteBoolValue(false);
                        break;

                    case JsonNodeType.True:
                        jsonWriter.WriteBoolValue(true);
                        break;

                    case JsonNodeType.Number64:
                        {
                            Number64 value = JsonBinaryEncoding.GetNumberValue(buffer.Span);
                            jsonWriter.WriteNumber64Value(value);
                        }
                        break;

                    case JsonNodeType.String:
                    case JsonNodeType.FieldName:
                        bool fieldName = binaryNavigatorNode.JsonNodeType == JsonNodeType.FieldName;

                        Utf8Memory utf8Buffer = Utf8Memory.UnsafeCreateNoValidation(buffer);
                        if (JsonBinaryEncoding.TryGetBufferedStringValue(
                            utf8Buffer,
                            this.jsonStringDictionary,
                            out Utf8Memory bufferedStringValue))
                        {
                            if (fieldName)
                            {
                                jsonWriter.WriteFieldName(bufferedStringValue.Span);
                            }
                            else
                            {
                                jsonWriter.WriteStringValue(bufferedStringValue.Span);
                            }
                        }
                        else
                        {
                            string value = JsonBinaryEncoding.GetStringValue(
                                utf8Buffer,
                                this.jsonStringDictionary);
                            if (fieldName)
                            {
                                jsonWriter.WriteFieldName(value);
                            }
                            else
                            {
                                jsonWriter.WriteStringValue(value);
                            }
                        }
                        break;

                    case JsonNodeType.Array:
                        {
                            jsonWriter.WriteArrayStart();

                            foreach (BinaryNavigatorNode arrayItem in this.GetArrayItemsInternal(buffer))
                            {
                                this.WriteToInternal(arrayItem, jsonWriter);
                            }

                            jsonWriter.WriteArrayEnd();
                        }
                        break;

                    case JsonNodeType.Object:
                        {
                            jsonWriter.WriteObjectStart();

                            foreach (ObjectPropertyInternal objectProperty in this.GetObjectPropertiesInternal(buffer))
                            {
                                this.WriteToInternal(objectProperty.NameNode, jsonWriter);
                                this.WriteToInternal(objectProperty.ValueNode, jsonWriter);
                            }

                            jsonWriter.WriteObjectEnd();
                        }
                        break;

                    case JsonNodeType.Int8:
                        {
                            sbyte value = JsonBinaryEncoding.GetInt8Value(buffer.Span);
                            jsonWriter.WriteInt8Value(value);
                        }
                        break;

                    case JsonNodeType.Int16:
                        {
                            short value = JsonBinaryEncoding.GetInt16Value(buffer.Span);
                            jsonWriter.WriteInt16Value(value);
                        }
                        break;

                    case JsonNodeType.Int32:
                        {
                            int value = JsonBinaryEncoding.GetInt32Value(buffer.Span);
                            jsonWriter.WriteInt32Value(value);
                        }
                        break;

                    case JsonNodeType.Int64:
                        {
                            long value = JsonBinaryEncoding.GetInt64Value(buffer.Span);
                            jsonWriter.WriteInt64Value(value);
                        }
                        break;

                    case JsonNodeType.UInt32:
                        {
                            uint value = JsonBinaryEncoding.GetUInt32Value(buffer.Span);
                            jsonWriter.WriteUInt32Value(value);
                        }
                        break;

                    case JsonNodeType.Float32:
                        {
                            float value = JsonBinaryEncoding.GetFloat32Value(buffer.Span);
                            jsonWriter.WriteFloat32Value(value);
                        }
                        break;

                    case JsonNodeType.Float64:
                        {
                            double value = JsonBinaryEncoding.GetFloat64Value(buffer.Span);
                            jsonWriter.WriteFloat64Value(value);
                        }
                        break;

                    case JsonNodeType.Binary:
                        {
                            ReadOnlyMemory<byte> value = JsonBinaryEncoding.GetBinaryValue(buffer);
                            jsonWriter.WriteBinaryValue(value.Span);
                        }
                        break;

                    case JsonNodeType.Guid:
                        {
                            Guid value = JsonBinaryEncoding.GetGuidValue(buffer.Span);
                            jsonWriter.WriteGuidValue(value);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown {nameof(JsonNodeType)}: {nodeType}.");
                }
            }

            private readonly struct BinaryNavigatorNode : IJsonNavigatorNode
            {
                public BinaryNavigatorNode(
                    ReadOnlyMemory<byte> buffer,
                    JsonNodeType jsonNodeType)
                {
                    this.Buffer = buffer;
                    this.JsonNodeType = jsonNodeType;
                }

                public ReadOnlyMemory<byte> Buffer { get; }

                public JsonNodeType JsonNodeType { get; }
            }

            private readonly struct ObjectPropertyInternal
            {
                public ObjectPropertyInternal(
                    BinaryNavigatorNode nameNode,
                    BinaryNavigatorNode valueNode)
                {
                    this.NameNode = nameNode;
                    this.ValueNode = valueNode;
                }

                public BinaryNavigatorNode NameNode { get; }
                public BinaryNavigatorNode ValueNode { get; }
            }
        }
    }
}
