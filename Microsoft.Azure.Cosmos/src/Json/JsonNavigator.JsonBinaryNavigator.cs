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
    using static Microsoft.Azure.Cosmos.Json.JsonBinaryEncoding;

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
            private readonly ReadOnlyMemory<byte> rootBuffer;
            private readonly IJsonNavigatorNode rootNode;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryNavigator class
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            public JsonBinaryNavigator(ReadOnlyMemory<byte> buffer)
            {
                if (buffer.Length < 2)
                {
                    throw new ArgumentException($"{nameof(buffer)} must have at least two byte.");
                }

                if (buffer.Span[0] != (byte)JsonSerializationFormat.Binary)
                {
                    throw new ArgumentNullException("buffer must be binary encoded.");
                }

                this.rootBuffer = buffer;

                // offset for the 0x80 (128) binary serialization type marker.
                buffer = buffer.Slice(1);

                // Only navigate the outer most JSON value and trim off trailing bytes
                int jsonValueLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                if (buffer.Length < jsonValueLength)
                {
                    throw new ArgumentException("Input buffer is shorter than the root node length.");
                }

                JsonNodeType nodeType = JsonBinaryEncoding.NodeTypes.Lookup[buffer.Span[0]];
                this.rootNode = new BinaryNavigatorNode(nodeType, 1);
            }

            #region IJsonNavigator

            /// <inheritdoc />
            public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Binary;

            /// <inheritdoc />
            public override IJsonNavigatorNode GetRootNode()
            {
                return this.rootNode;
            }

            /// <inheritdoc />
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (!(node is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(BinaryNavigatorNode)}");
                }

                return binaryNavigatorNode.NodeType;
            }

            /// <inheritdoc />
            public override Number64 GetNumber64Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Number64, numberNode);
                return JsonBinaryEncoding.GetNumberValue(
                    this.GetBufferAt(binaryNavigatorNode.Offset),
                    binaryNavigatorNode.ExternalArrayInfo);
            }

            /// <inheritdoc />
            public override bool TryGetBufferedStringValue(
                IJsonNavigatorNode stringNode,
                out Utf8Memory value)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.String, stringNode);
                return JsonBinaryEncoding.TryGetBufferedStringValue(
                    this.rootBuffer,
                    this.rootBuffer.Slice(binaryNavigatorNode.Offset),
                    out value);
            }

            /// <inheritdoc />
            public override UtfAnyString GetStringValue(IJsonNavigatorNode stringNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.String, stringNode);
                return JsonBinaryEncoding.GetUtf8StringValue(
                    this.rootBuffer,
                    this.rootBuffer.Slice(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override sbyte GetInt8Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Int8, numberNode);
                return JsonBinaryEncoding.GetInt8Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override short GetInt16Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Int16, numberNode);
                return JsonBinaryEncoding.GetInt16Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override int GetInt32Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Int32, numberNode);
                return JsonBinaryEncoding.GetInt32Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override long GetInt64Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Int64, numberNode);
                return JsonBinaryEncoding.GetInt64Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override float GetFloat32Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Float32, numberNode);
                return JsonBinaryEncoding.GetFloat32Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override double GetFloat64Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Float64, numberNode);
                return JsonBinaryEncoding.GetFloat64Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override uint GetUInt32Value(IJsonNavigatorNode numberNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.UInt32, numberNode);
                return JsonBinaryEncoding.GetUInt32Value(this.GetBufferAt(binaryNavigatorNode.Offset));
            }

            /// <inheritdoc />
            public override Guid GetGuidValue(IJsonNavigatorNode guidNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Guid, guidNode);
                return JsonBinaryEncoding.GetGuidValue(this.GetBufferAt(binaryNavigatorNode.Offset));
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
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Binary, binaryNode);
                bufferedBinaryValue = JsonBinaryEncoding.GetBinaryValue(this.rootBuffer.Slice(binaryNavigatorNode.Offset));
                return true;
            }

            /// <inheritdoc />
            public override int GetArrayItemCount(IJsonNavigatorNode arrayNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Array, arrayNode);
                ReadOnlyMemory<byte> buffer = this.rootBuffer.Slice(binaryNavigatorNode.Offset);

                int length;
                long count;
                byte typeMarker = buffer.Span[0];
                switch (typeMarker)
                {
                    // Empty and Single Array
                    case TypeMarker.Arr0:
                        count = 0;
                        break;
                    case TypeMarker.Arr1:
                        count = 1;
                        break;

                    // Arrays with length and count prefix
                    case TypeMarker.ArrLC1:
                        count = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(1 + 1).Span);
                        break;
                    case TypeMarker.ArrLC2:
                        count = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(1 + 2).Span);
                        break;
                    case TypeMarker.ArrLC4:
                        count = JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(1 + 4).Span);
                        break;

                    // Arrays with length prefix
                    case TypeMarker.ArrL1:
                        length = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 1, length).Span);
                        break;
                    case TypeMarker.ArrL2:
                        length = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 2, length).Span);
                        break;
                    case TypeMarker.ArrL4:
                        length = (int)JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 4, length).Span);
                        break;

                    case TypeMarker.ArrNumC1:
                    case TypeMarker.ArrNumC2:
                    case TypeMarker.ArrArrNumC1C1:
                    case TypeMarker.ArrArrNumC2C2:
                        count = JsonBinaryEncoding.GetUniformArrayItemCount(this.GetBufferAt(binaryNavigatorNode.Offset));
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected array type marker: {typeMarker}");
                }

                if (count > int.MaxValue)
                {
                    throw new InvalidOperationException("Array item count can not be more than 32-bit integer maximum value.");
                }

                return (int)count;
            }

            /// <inheritdoc />
            public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index)
            {
                if (index < 0)
                {
                    throw new IndexOutOfRangeException();
                }

                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Array, arrayNode);

                // TODO (brchon): We can optimize for the case where the count is serialized so we can avoid using the linear time call to TryGetValueAt().
                IEnumerable<Enumerator.ArrayItem> arrayItems = Enumerator.GetArrayItems(
                    this.rootBuffer,
                    binaryNavigatorNode.Offset,
                    binaryNavigatorNode.ExternalArrayInfo);

                arrayItems = arrayItems.Skip(index);
                if (!arrayItems.Any())
                {
                    throw new IndexOutOfRangeException($"The specified array index '{index}' is out of range.");
                }

                Enumerator.ArrayItem arrayItem = arrayItems.First();
                JsonNodeType nodeType = this.GetNodeType(arrayItem.Offset, arrayItem.ExternalArrayInfo);
                return new BinaryNavigatorNode(nodeType, arrayItem.Offset, arrayItem.ExternalArrayInfo);
            }

            /// <inheritdoc />
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Array, arrayNode);
                return this.GetArrayItemsInternal(binaryNavigatorNode).Select((node) => (IJsonNavigatorNode)node);
            }

            /// <inheritdoc />
            public override int GetObjectPropertyCount(IJsonNavigatorNode objectNode)
            {
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Object, objectNode);
                ReadOnlyMemory<byte> buffer = this.rootBuffer.Slice(binaryNavigatorNode.Offset);

                int length;
                long count;
                byte typeMarker = buffer.Span[0];
                switch (typeMarker)
                {
                    // Empty and Single Object
                    case TypeMarker.Obj0:
                        count = 0;
                        break;
                    case TypeMarker.Obj1:
                        count = 1;
                        break;

                    // Object with length and count prefix
                    case TypeMarker.ObjLC1:
                        count = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(1 + 1).Span);
                        break;
                    case TypeMarker.ObjLC2:
                        count = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(1 + 2).Span);
                        break;
                    case TypeMarker.ObjLC4:
                        count = JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(1 + 4).Span);
                        break;

                    // Object with length prefix
                    case TypeMarker.ObjL1:
                        length = JsonBinaryEncoding.GetFixedSizedValue<byte>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 1, length).Span) / 2;
                        break;
                    case TypeMarker.ObjL2:
                        length = JsonBinaryEncoding.GetFixedSizedValue<ushort>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 2, length).Span) / 2;
                        break;
                    case TypeMarker.ObjL4:
                        length = (int)JsonBinaryEncoding.GetFixedSizedValue<uint>(buffer.Slice(1).Span);
                        count = JsonBinaryNavigator.GetValueCount(buffer.Slice(1 + 4, length).Span) / 2;
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected object type marker: {typeMarker}");
                }

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
                _ = this.GetNodeOfType(JsonNodeType.Object, objectNode);

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
                BinaryNavigatorNode binaryNavigatorNode = this.GetNodeOfType(JsonNodeType.Object, objectNode);
                return this.GetObjectPropertiesInternal(binaryNavigatorNode)
                    .Select((objectPropertyInternal) => new ObjectProperty(
                        objectPropertyInternal.NameNode,
                        objectPropertyInternal.ValueNode));
            }

            public override IJsonReader CreateReader(IJsonNavigatorNode jsonNavigatorNode)
            {
                if (!(jsonNavigatorNode is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(jsonNavigatorNode)} must be a {nameof(BinaryNavigatorNode)}");
                }

                ReadOnlyMemory<byte> buffer = this.rootBuffer.Slice(binaryNavigatorNode.Offset);
                if (!MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                {
                    throw new InvalidOperationException("Failed to get segment");
                }

                return JsonReader.CreateBinaryFromOffset(this.rootBuffer, segment.Offset);
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
                    bool isFieldName = binaryNavigatorNode.NodeType == JsonNodeType.FieldName;
                    if (!(jsonWriter is IJsonBinaryWriterExtensions jsonBinaryWriter))
                    {
                        throw new InvalidOperationException($"Expected writer to implement: {nameof(IJsonBinaryWriterExtensions)}.");
                    }

                    jsonBinaryWriter.WriteRawJsonValue(
                        this.rootBuffer,
                        valueOffset: binaryNavigatorNode.Offset,
                        externalArrayInfo: binaryNavigatorNode.ExternalArrayInfo,
                        isFieldName);
                }
                else
                {
                    this.WriteToInternal(binaryNavigatorNode, jsonWriter);
                }
            }
            #endregion

            private IEnumerable<BinaryNavigatorNode> GetArrayItemsInternal(BinaryNavigatorNode arrayNode)
            {
                return Enumerator
                .GetArrayItems(this.rootBuffer, arrayNode.Offset, arrayNode.ExternalArrayInfo)
                .Select(arrayItem => new BinaryNavigatorNode(
                    this.GetNodeType(arrayItem.Offset, arrayItem.ExternalArrayInfo),
                    arrayItem.Offset,
                    arrayItem.ExternalArrayInfo));
            }

            private IEnumerable<ObjectPropertyInternal> GetObjectPropertiesInternal(BinaryNavigatorNode objectNode)
            {
                return JsonBinaryEncoding.Enumerator
                .GetObjectProperties(this.rootBuffer, objectNode.Offset)
                .Select(property => new ObjectPropertyInternal(
                    new BinaryNavigatorNode(
                        JsonNodeType.FieldName,
                        property.NameOffset),
                    new BinaryNavigatorNode(
                        this.GetNodeType(property.ValueOffset, externalArrayInfo: default),
                        property.ValueOffset)));
            }

            private void WriteToInternal(BinaryNavigatorNode binaryNavigatorNode, IJsonWriter jsonWriter)
            {
                ReadOnlyMemory<byte> buffer = this.rootBuffer.Slice(binaryNavigatorNode.Offset);
                JsonNodeType nodeType = binaryNavigatorNode.NodeType;

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
                            Number64 value = JsonBinaryEncoding.GetNumberValue(
                                buffer.Span,
                                binaryNavigatorNode.ExternalArrayInfo);

                            jsonWriter.WriteNumber64Value(value);
                        }
                        break;

                    case JsonNodeType.String:
                    case JsonNodeType.FieldName:
                        bool fieldName = binaryNavigatorNode.NodeType == JsonNodeType.FieldName;

                        if (JsonBinaryEncoding.TryGetBufferedStringValue(
                            this.rootBuffer,
                            buffer,
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
                            string value = JsonBinaryEncoding.GetStringValue(this.rootBuffer, buffer);
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

                            foreach (BinaryNavigatorNode arrayItem in this.GetArrayItemsInternal(binaryNavigatorNode))
                            {
                                this.WriteToInternal(arrayItem, jsonWriter);
                            }

                            jsonWriter.WriteArrayEnd();
                        }
                        break;

                    case JsonNodeType.Object:
                        {
                            jsonWriter.WriteObjectStart();

                            foreach (ObjectPropertyInternal objectProperty in this.GetObjectPropertiesInternal(binaryNavigatorNode))
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

            private BinaryNavigatorNode GetNodeOfType(
                JsonNodeType nodeType,
                IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (!(node is BinaryNavigatorNode binaryNavigatorNode))
                {
                    throw new ArgumentException($"{nameof(node)} must be a {nameof(BinaryNavigatorNode)}.");
                }

                JsonNodeType actualNodeType = this.GetNodeType(binaryNavigatorNode.Offset, binaryNavigatorNode.ExternalArrayInfo);
                if (actualNodeType != nodeType)
                {
                    throw new ArgumentException($"Node needs to be of type {nodeType}.");
                }

                return binaryNavigatorNode;
            }

            private ReadOnlySpan<byte> GetBufferAt(int offset)
            {
                return offset > 0 ? this.rootBuffer.Slice(offset).Span : default;
            }

            private JsonNodeType GetNodeType(int offset, UniformArrayInfo externalArrayInfo)
            {
                JsonNodeType nodeType;

                if (externalArrayInfo != null)
                {
                    switch (externalArrayInfo.ItemTypeMarker)
                    {
                        case TypeMarker.Int8:
                        case TypeMarker.Int16:
                        case TypeMarker.Int32:
                        case TypeMarker.Int64:
                        case TypeMarker.UInt8:
                        case TypeMarker.Float16:
                        case TypeMarker.Float32:
                        case TypeMarker.Float64:
                            nodeType = JsonNodeType.Number64;
                            break;

                        case TypeMarker.ArrNumC1:
                        case TypeMarker.ArrNumC2:
                            nodeType = JsonNodeType.Array;
                            break;

                        default:
                            throw new InvalidOperationException();
                    }
                }
                else
                {
                    byte typeMarker = this.rootBuffer.Span[offset];
                    nodeType = JsonBinaryEncoding.NodeTypes.Lookup[typeMarker];
                }

                return nodeType;
            }

            private readonly struct BinaryNavigatorNode : IJsonNavigatorNode
            {
                public BinaryNavigatorNode(
                    JsonNodeType nodeType,
                    int offset,
                    UniformArrayInfo externalArrayInfo = default)
                {
                    this.NodeType = nodeType;
                    this.Offset = offset;
                    this.ExternalArrayInfo = externalArrayInfo;
                }

                public JsonNodeType NodeType { get; }

                public int Offset { get; }

                public UniformArrayInfo ExternalArrayInfo { get; }
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
