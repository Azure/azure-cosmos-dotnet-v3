//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;

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
            private readonly BinaryNode rootNode;
            private readonly LittleEndianBinaryReader binaryReader;
            private readonly byte[] buffer;
            private readonly JsonStringDictionary jsonStringDictionary;

            /// <summary>
            /// Initializes a new instance of the JsonBinaryNavigator class
            /// </summary>
            /// <param name="buffer">The (UTF-8) buffer to navigate.</param>
            /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
            /// <param name="skipValidation">whether to skip validation or not.</param>
            public JsonBinaryNavigator(byte[] buffer, JsonStringDictionary jsonStringDictionary, bool skipValidation = false)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException($"{nameof(buffer)} can not be null");
                }

                if (buffer.Length < 1)
                {
                    throw new ArgumentException($"{nameof(buffer)} must have at least one byte.");
                }

                this.rootNode = new BinaryNode(1, JsonBinaryEncoding.GetNodeType(buffer[1]));

                // false, since stream is not writeable
                // true, since buffer is visible
                this.binaryReader = new LittleEndianBinaryReader(new MemoryStream(buffer, 0, buffer.Length, false, true));
                this.buffer = buffer;
                this.jsonStringDictionary = jsonStringDictionary;
            }

            /// <summary>
            /// Gets the <see cref="JsonSerializationFormat"/> for the IJsonNavigator.
            /// </summary>
            public override JsonSerializationFormat SerializationFormat
            {
                get
                {
                    return JsonSerializationFormat.Binary;
                }
            }

            /// <summary>
            /// Gets <see cref="IJsonNavigatorNode"/> of the root node.
            /// </summary>
            /// <returns><see cref="IJsonNavigatorNode"/> corresponding to the root node.</returns>
            public override IJsonNavigatorNode GetRootNode()
            {
                return this.rootNode;
            }

            /// <summary>
            /// Gets the <see cref="JsonNodeType"/> type for a particular node
            /// </summary>
            /// <param name="node">The <see cref="IJsonNavigatorNode"/> of the node you want to know the type of</param>
            /// <returns><see cref="JsonNodeType"/> for the node</returns>
            public override JsonNodeType GetNodeType(IJsonNavigatorNode node)
            {
                if (node == null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if ((node as BinaryNode) == null)
                {
                    throw new ArgumentException($"{nameof(node)} must be a binary node.");
                }

                return ((BinaryNode)node).JsonNodeType;
            }

            /// <summary>
            /// Gets the numeric value for a node
            /// </summary>
            /// <param name="numberNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the node you want the number value from.</param>
            /// <returns>A double that represents the number value in the node.</returns>
            public override double GetNumberValue(IJsonNavigatorNode numberNavigatorNode)
            {
                if (numberNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(numberNavigatorNode));
                }

                if (!(((numberNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Number))
                {
                    throw new ArgumentException($"{nameof(numberNavigatorNode)} must be a binary number node.");
                }

                long offset = ((BinaryNode)numberNavigatorNode).Offset;
                this.binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                return JsonBinaryEncoding.GetNumberValue(this.binaryReader);
            }

            /// <summary>
            /// Tries to get the buffered string value from a node.
            /// </summary>
            /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the buffered string from.</param>
            /// <param name="bufferedStringValue">The buffered string value if possible</param>
            /// <returns><code>true</code> if the JsonNavigator successfully got the buffered string value; <code>false</code> if the JsonNavigator failed to get the buffered string value.</returns>
            public override bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out IReadOnlyList<byte> bufferedStringValue)
            {
                //TODO (brchon): implement this when optimizing code.
                bufferedStringValue = null;
                return false;
            }

            /// <summary>
            /// Gets a string value from a node.
            /// </summary>
            /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the string value from.</param>
            /// <returns>The string value from the node.</returns>
            public override string GetStringValue(IJsonNavigatorNode stringNode)
            {
                if (stringNode == null)
                {
                    throw new ArgumentNullException(nameof(stringNode));
                }

                if (!((((stringNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.String) ||
                    (((stringNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.FieldName)))
                {
                    throw new ArgumentException($"{nameof(stringNode)} must be a binary string or fieldname node.");
                }

                long offset = ((BinaryNode)stringNode).Offset;
                this.binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                return JsonBinaryEncoding.GetStringValue(this.binaryReader, this.jsonStringDictionary);
            }

            public override sbyte GetInt8Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override short GetInt16Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override int GetInt32Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override long GetInt64Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override float GetFloat32Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override double GetFloat64Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override uint GetUInt32Value(IJsonNavigatorNode numberNode)
            {
                throw new NotImplementedException();
            }

            public override Guid GetGuidValue(IJsonNavigatorNode guidNode)
            {
                throw new NotImplementedException();
            }

            public override IReadOnlyList<byte> GetBinaryValue(IJsonNavigatorNode binaryNode)
            {
                throw new NotImplementedException();
            }

            public override bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out IReadOnlyList<byte> bufferedBinaryValue)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Gets the number of elements in an array node.
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to get the count of.</param>
            /// <returns>The number of elements in the array node.</returns>
            public override int GetArrayItemCount(IJsonNavigatorNode arrayNavigatorNode)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(arrayNavigatorNode));
                }

                if (!(((arrayNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Array))
                {
                    throw new ArgumentException($"{nameof(arrayNavigatorNode)} must be a binary array node.");
                }

                int offset = ((BinaryNode)arrayNavigatorNode).Offset;
                byte typeMarker = this.buffer[offset];
                int firstValueOffset = offset + JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
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
                        count = this.buffer[offset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength];
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                        count = BitConverter.ToUInt16(this.buffer, offset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        count = BitConverter.ToUInt32(this.buffer, offset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength);
                        break;

                    // Arrays with length prefix
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                        count = this.GetValueCount(firstValueOffset, this.buffer[offset + JsonBinaryEncoding.TypeMarkerLength]);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                        count = this.GetValueCount(firstValueOffset, BitConverter.ToUInt16(this.buffer, offset + JsonBinaryEncoding.TypeMarkerLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                        count = this.GetValueCount(firstValueOffset, BitConverter.ToUInt32(this.buffer, offset + JsonBinaryEncoding.TypeMarkerLength));
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

            /// <summary>
            /// Gets the node at a particular index of an array node
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to index from.</param>
            /// <param name="index">The offset into the array</param>
            /// <returns>The <see cref="IJsonNavigatorNode"/> of the node at a particular index of an array node</returns>
            public override IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNavigatorNode, int index)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(arrayNavigatorNode));
                }

                if (!(((arrayNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Array))
                {
                    throw new ArgumentException($"{nameof(arrayNavigatorNode)} must be a binary array node.");
                }

                // TODO (brchon): We can optimize for the case where the count is serialized so we can avoid using the linear time call to TryGetValueAt().

                int arrayOffset = ((BinaryNode)arrayNavigatorNode).Offset;
                byte typeMarker = this.buffer[arrayOffset];

                BinaryNode node;
                long firstArrayItemOffset = arrayOffset + JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                long arrayLength = JsonBinaryEncoding.GetValueLength(this.buffer, arrayOffset);
                long arrayItemsLength = arrayLength - (firstArrayItemOffset - arrayOffset);
                if (!this.TryGetValueAt(firstArrayItemOffset, arrayItemsLength, index, out node))
                {
                    throw new IndexOutOfRangeException($"Tried to access index:{index} in an array.");
                }

                return node;
            }

            /// <summary>
            /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.
            /// </summary>
            /// <param name="arrayNavigatorNode">The <see cref="IJsonNavigatorNode"/> of the array to get the items from</param>
            /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.</returns>
            public override IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNavigatorNode)
            {
                if (arrayNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(arrayNavigatorNode));
                }

                if (!(((arrayNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Array))
                {
                    throw new ArgumentException($"{nameof(arrayNavigatorNode)} must be a binary array node.");
                }

                int arrayOffset = ((BinaryNode)arrayNavigatorNode).Offset;
                byte typeMarker = this.buffer[arrayOffset];

                long arrayLength = JsonBinaryEncoding.GetValueLength(this.buffer, arrayOffset);
                long firstValueOffset = arrayOffset + JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                long bytesToProcess = (arrayOffset + arrayLength) - firstValueOffset;

                for (int bytesProcessed = 0; bytesProcessed < bytesToProcess; bytesProcessed += (int)JsonBinaryEncoding.GetValueLength(this.buffer, firstValueOffset + bytesProcessed))
                {
                    yield return this.CreateBinaryNode((int)(firstValueOffset + bytesProcessed));
                }
            }

            /// <summary>
            /// Gets the number of properties in an object node.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="IJsonNavigatorNode"/> of node to get the property count from.</param>
            /// <returns>The number of properties in an object node.</returns>
            public override int GetObjectPropertyCount(IJsonNavigatorNode objectNavigatorNode)
            {
                if (objectNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(objectNavigatorNode));
                }

                if (!(((objectNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Object))
                {
                    throw new ArgumentException($"{nameof(objectNavigatorNode)} must be a binary object node.");
                }

                int objectOffset = ((BinaryNode)objectNavigatorNode).Offset;
                byte typeMarker = this.buffer[objectOffset];
                int firstObjectPropertyOffset = objectOffset + JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
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
                        count = this.buffer[objectOffset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength];
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                        count = BitConverter.ToUInt16(this.buffer, objectOffset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        count = BitConverter.ToUInt32(this.buffer, objectOffset + JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength);
                        break;

                    // Object with length prefix
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                        count = this.GetValueCount(firstObjectPropertyOffset, this.buffer[objectOffset + JsonBinaryEncoding.TypeMarkerLength]);
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                        count = this.GetValueCount(firstObjectPropertyOffset, BitConverter.ToUInt16(this.buffer, objectOffset + JsonBinaryEncoding.TypeMarkerLength));
                        break;
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                        count = this.GetValueCount(firstObjectPropertyOffset, BitConverter.ToUInt32(this.buffer, objectOffset + JsonBinaryEncoding.TypeMarkerLength));
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

            /// <summary>
            /// Tries to get a object property from an object with a particular property name.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="ObjectProperty"/> of object node to get a property from.</param>
            /// <param name="propertyName">The name of the property to search for.</param>
            /// <param name="objectProperty">The <see cref="IJsonNavigatorNode"/> with the specified property name if it exists.</param>
            /// <returns><code>true</code> if the JsonNavigator successfully found the <see cref="ObjectProperty"/> with the specified property name; <code>false</code> otherwise.</returns>
            public override bool TryGetObjectProperty(IJsonNavigatorNode objectNavigatorNode, string propertyName, out ObjectProperty objectProperty)
            {
                foreach (ObjectProperty objectPropertyNode in this.GetObjectProperties(objectNavigatorNode))
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

            /// <summary>
            /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/> properties from an object node.
            /// </summary>
            /// <param name="objectNavigatorNode">The <see cref="IJsonNavigatorNode"/> of object node to get the properties from.</param>
            /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/> properties from an object node.</returns>
            public override IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNavigatorNode)
            {
                if (objectNavigatorNode == null)
                {
                    throw new ArgumentNullException(nameof(objectNavigatorNode));
                }

                if (!(((objectNavigatorNode as BinaryNode)?.JsonNodeType ?? JsonNodeType.Unknown) == JsonNodeType.Object))
                {
                    throw new ArgumentException($"{nameof(objectNavigatorNode)} must actually be a binary object node.");
                }

                int offset = ((BinaryNode)objectNavigatorNode).Offset;
                byte typeMarker = this.buffer[offset];
                long objectLength = JsonBinaryEncoding.GetValueLength(this.buffer, offset);
                long firstValueOffset = offset + JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                long bytesToProcess = (offset + objectLength) - firstValueOffset;
                for (int bytesProcessed = 0; bytesProcessed < bytesToProcess;)
                {
                    BinaryNode nameNode = new BinaryNode((int)(firstValueOffset + bytesProcessed), JsonNodeType.FieldName);
                    bytesProcessed += (int)JsonBinaryEncoding.GetValueLength(this.buffer, firstValueOffset + bytesProcessed);
                    BinaryNode valueNode = this.CreateBinaryNode((int)(firstValueOffset + bytesProcessed));
                    bytesProcessed += (int)JsonBinaryEncoding.GetValueLength(this.buffer, firstValueOffset + bytesProcessed);
                    yield return new ObjectProperty(nameNode, valueNode);
                }
            }

            /// <summary>
            /// Tries to get the buffered raw json
            /// </summary>
            /// <param name="jsonNode">The json node of interest</param>
            /// <param name="bufferedRawJson">The raw json.</param>
            /// <returns>True if bufferedRawJson was set. False otherwise.</returns>
            public override bool TryGetBufferedRawJson(
                IJsonNavigatorNode jsonNode,
                out IReadOnlyList<byte> bufferedRawJson)
            {
                if (jsonNode == null || !(jsonNode is BinaryNode jsonBinaryNode))
                {
                    bufferedRawJson = default(IReadOnlyList<byte>);
                    return false;
                }

                int nodeLength = (int)JsonBinaryEncoding.GetValueLength(this.buffer, (long)jsonBinaryNode.Offset);
                bufferedRawJson = new ArraySegment<byte>(this.buffer, jsonBinaryNode.Offset, nodeLength);

                return true;
            }

            private int GetValueCount(long offset, long length)
            {
                long bytesProcessed = 0;
                int count = 0;
                while (bytesProcessed < length)
                {
                    count++;
                    bytesProcessed += JsonBinaryEncoding.GetValueLength(this.buffer, offset + bytesProcessed);
                }

                return count;
            }

            private bool TryGetValueAt(long offset, long length, long index, out BinaryNode node)
            {
                long currentOffset = offset;
                for (long count = 0; count < index; count++)
                {
                    long valueLength = JsonBinaryEncoding.GetValueLength(this.buffer, currentOffset);
                    if (valueLength == 0)
                    {
                        node = default(BinaryNode);
                        return false;
                    }

                    currentOffset += valueLength;
                    if (currentOffset >= (offset + length))
                    {
                        node = default(BinaryNode);
                        return false;
                    }
                }

                if (currentOffset > int.MaxValue)
                {
                    throw new InvalidOperationException($"{nameof(currentOffset)} is greater than int.MaxValue");
                }

                node = this.CreateBinaryNode((int)currentOffset);
                return true;
            }

            private BinaryNode CreateBinaryNode(int offset)
            {
                return new BinaryNode(offset, JsonBinaryEncoding.GetNodeType(this.buffer[offset]));
            }

            private class BinaryNode : IJsonNavigatorNode
            {
                public BinaryNode(int offset, JsonNodeType jsonNodeType)
                {
                    this.Offset = offset;
                    this.JsonNodeType = jsonNodeType;
                }

                public int Offset { get; }

                public JsonNodeType JsonNodeType { get; }
            }
        }
    }
}
