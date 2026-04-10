//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// JsonNavigator interface for classes that can navigate jsons.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    interface IJsonNavigator
    {
        /// <summary>
        /// Gets the <see cref="JsonSerializationFormat"/> for the IJsonNavigator.
        /// </summary>
        JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Gets the root node.
        /// </summary>
        /// <returns>The root node.</returns>
        IJsonNavigatorNode GetRootNode();

        /// <summary>
        /// Gets the <see cref="JsonNodeType"/> type for a particular node
        /// </summary>
        /// <param name="node">The the node you want to know the type of</param>
        /// <returns><see cref="JsonNodeType"/> for the node</returns>
        JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <summary>
        /// Gets the numeric value for a node
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A double that represents the number value in the node.</returns>
        Number64 GetNumberValue(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Tries to get the buffered string value from a node.
        /// </summary>
        /// <param name="stringNode">The node to get the buffered string from.</param>
        /// <param name="value">The buffered string value if possible</param>
        /// <returns><code>true</code> if the JsonNavigator successfully got the buffered string value; <code>false</code> if the JsonNavigator failed to get the buffered string value.</returns>
        bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory value);

        /// <summary>
        /// Gets a string value from a node.
        /// </summary>
        /// <param name="stringNode">The node to get the string value from.</param>
        /// <returns>The string value from the node.</returns>
        UtfAnyString GetStringValue(IJsonNavigatorNode stringNode);

        /// <summary>
        /// Gets the numeric value for a node as a signed byte.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A sbyte value that represents the number value in the node.</returns>
        sbyte GetInt8Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as a 16-bit signed integer.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A short value that represents the number value in the node.</returns>
        short GetInt16Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as a 32-bit signed integer.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>An int value that represents the number value in the node.</returns>
        int GetInt32Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as a 64-bit signed integer.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A long value that represents the number value in the node.</returns>
        long GetInt64Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as a single precision number if the number is expressed as a floating point.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A double that represents the number value in the node.</returns>
        float GetFloat32Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as double precision number if the number is expressed as a floating point.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>A double that represents the number value in the node.</returns>
        double GetFloat64Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the numeric value for a node as an unsigned 32 bit integer if the node is expressed as an uint32.
        /// </summary>
        /// <param name="numberNode">The node you want the number value from.</param>
        /// <returns>An unsigned integer that represents the number value in the node.</returns>
        uint GetUInt32Value(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Gets the Guid value for a node.
        /// </summary>
        /// <param name="guidNode">The node you want the guid value from.</param>
        /// <returns>A guid read from the node.</returns>
        Guid GetGuidValue(IJsonNavigatorNode guidNode);

        /// <summary>
        /// Gets a binary value for a given node from the input.
        /// </summary>
        /// <param name="binaryNode">The node to get the binary value from.</param>
        /// <returns>The binary value from the node</returns>
        ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode);

        /// <summary>
        /// Tries to get the buffered binary value from a node.
        /// </summary>
        /// <param name="binaryNode">The node to get the buffered binary from.</param>
        /// <param name="bufferedBinaryValue">The buffered binary value if possible</param>
        /// <returns><code>true</code> if the JsonNavigator successfully got the buffered binary value; <code>false</code> if the JsonNavigator failed to get the buffered binary value.</returns>
        bool TryGetBufferedBinaryValue(
            IJsonNavigatorNode binaryNode,
            out ReadOnlyMemory<byte> bufferedBinaryValue);

        /// <summary>
        /// Gets the number of elements in an array node.
        /// </summary>
        /// <param name="arrayNode">The (array) node to get the count of.</param>
        /// <returns>The number of elements in the array node.</returns>
        int GetArrayItemCount(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the node at a particular index of an array node
        /// </summary>
        /// <param name="arrayNode">The (array) node to index from.</param>
        /// <param name="index">The offset into the array</param>
        /// <returns>The node at a particular index of an array node</returns>
        IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index);

        /// <summary>
        /// Gets the array item nodes of the array node.
        /// </summary>
        /// <param name="arrayNode">The array to get the items from.</param>
        /// <returns>The array item nodes of the array node</returns>
        IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the number of properties in an object node.
        /// </summary>
        /// <param name="objectNode">The node to get the property count from.</param>
        /// <returns>The number of properties in an object node.</returns>
        int GetObjectPropertyCount(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Tries to get a object property from an object with a particular property name.
        /// </summary>
        /// <param name="objectNode">The object node to get a property from.</param>
        /// <param name="propertyName">The name of the property to search for.</param>
        /// <param name="objectProperty">The <see cref="ObjectProperty"/> with the specified property name if it exists.</param>
        /// <returns><code>true</code> if the JsonNavigator successfully found the <see cref="ObjectProperty"/> with the specified property name; <code>false</code> otherwise.</returns>
        bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <summary>
        /// Gets the <see cref="ObjectProperty"/> properties from an object node.
        /// </summary>
        /// <param name="objectNode">The object node to get the properties from.</param>
        /// <returns>The <see cref="ObjectProperty"/> properties from an object node.</returns>
        IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Creates an <see cref="IJsonReader"/> that is able to read the supplied <see cref="IJsonNavigatorNode"/>.
        /// </summary>
        /// <param name="node">The node to create a reader from..</param>
        /// <returns>The <see cref="IJsonReader"/> that is able to read the supplied <see cref="IJsonNavigatorNode"/>.</returns>
        public IJsonReader CreateReader(IJsonNavigatorNode node);

        /// <summary>
        /// Writes a <see cref="IJsonNavigatorNode"/> to a <see cref="IJsonWriter"/>.
        /// </summary>
        /// <param name="node">The <see cref="IJsonNavigatorNode"/> to write.</param>
        /// <param name="jsonWriter">The <see cref="IJsonWriter"/> to write to.</param>
        void WriteNode(IJsonNavigatorNode node, IJsonWriter jsonWriter);
    }
}
