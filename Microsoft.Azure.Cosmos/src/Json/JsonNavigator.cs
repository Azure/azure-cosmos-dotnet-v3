//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Base abstract class for JSON navigators.
    /// The navigator defines methods that allow random access to JSON document nodes.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract partial class JsonNavigator : IJsonNavigator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNavigator"/> class.
        /// </summary>
        protected JsonNavigator()
        {
        }

        /// <summary>
        /// Gets the <see cref="JsonSerializationFormat"/> for the IJsonNavigator.
        /// </summary>
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Creates a JsonNavigator that can navigate a supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to navigate</param>
        /// <param name="jsonStringDictionary">The optional json string dictionary for binary encoding.</param>
        /// <param name="skipValidation">Whether validation should be skipped.</param>
        /// <returns>A concrete JsonNavigator that can navigate the supplied buffer.</returns>
        public static IJsonNavigator Create(byte[] buffer, JsonStringDictionary jsonStringDictionary = null, bool skipValidation = false)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            // Examine the first buffer byte to determine the serialization format
            byte firstByte = buffer[0];

            switch ((JsonSerializationFormat)firstByte)
            {
                // Explicitly pick from the set of supported formats
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryNavigator(buffer, jsonStringDictionary, skipValidation);
                default:
                    // or otherwise assume text format
                    return new JsonTextNavigator(buffer, skipValidation);
            }
        }

        /// <summary>
        /// Gets <see cref="IJsonNavigatorNode"/> of the root node.
        /// </summary>
        /// <returns><see cref="IJsonNavigatorNode"/> corresponding to the root node.</returns>
        public abstract IJsonNavigatorNode GetRootNode();

        /// <summary>
        /// Gets the <see cref="JsonNodeType"/> type for a particular node
        /// </summary>
        /// <param name="node">The <see cref="IJsonNavigatorNode"/> of the node you want to know the type of</param>
        /// <returns><see cref="JsonNodeType"/> for the node</returns>
        public abstract JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <summary>
        /// Gets the numeric value for a node
        /// </summary>
        /// <param name="numberNode">The <see cref="IJsonNavigatorNode"/> of the node you want the number value from.</param>
        /// <returns>A double that represents the number value in the node.</returns>
        public abstract double GetNumberValue(IJsonNavigatorNode numberNode);

        /// <summary>
        /// Tries to get the buffered string value from a node.
        /// </summary>
        /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the buffered string from.</param>
        /// <param name="bufferedStringValue">The buffered string value if possible</param>
        /// <returns><code>true</code> if the JsonNavigator successfully got the buffered string value; <code>false</code> if the JsonNavigator failed to get the buffered string value.</returns>
        public abstract bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out IReadOnlyList<byte> bufferedStringValue);

        /// <summary>
        /// Gets a string value from a node.
        /// </summary>
        /// <param name="stringNode">The <see cref="IJsonNavigatorNode"/> of the node to get the string value from.</param>
        /// <returns>The string value from the node.</returns>
        public abstract string GetStringValue(IJsonNavigatorNode stringNode);

        /// <inheritdoc />
        public abstract sbyte GetInt8Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract short GetInt16Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract int GetInt32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract long GetInt64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract float GetFloat32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract double GetFloat64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract uint GetUInt32Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract Guid GetGuidValue(IJsonNavigatorNode guidNode);

        /// <inheritdoc />
        public abstract IReadOnlyList<byte> GetBinaryValue(IJsonNavigatorNode binaryNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out IReadOnlyList<byte> bufferedBinaryValue);

        /// <summary>
        /// Gets the number of elements in an array node.
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to get the count of.</param>
        /// <returns>The number of elements in the array node.</returns>
        public abstract int GetArrayItemCount(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the node at a particular index of an array node
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the (array) node to index from.</param>
        /// <param name="index">The offset into the array</param>
        /// <returns>The <see cref="IJsonNavigatorNode"/> of the node at a particular index of an array node</returns>
        public abstract IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index);

        /// <summary>
        /// Gets an IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.
        /// </summary>
        /// <param name="arrayNode">The <see cref="IJsonNavigatorNode"/> of the array to get the items from</param>
        /// <returns>The IEnumerable of <see cref="IJsonNavigatorNode"/>s for an arrayNode.</returns>
        public abstract IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode);

        /// <summary>
        /// Gets the number of properties in an object node.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of node to get the property count from.</param>
        /// <returns>The number of properties in an object node.</returns>
        public abstract int GetObjectPropertyCount(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Tries to get a object property from an object with a particular property name.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of object node to get a property from.</param>
        /// <param name="propertyName">The name of the property to search for.</param>
        /// <param name="objectProperty">The <see cref="ObjectProperty"/> with the specified property name if it exists.</param>
        /// <returns><code>true</code> if the JsonNavigator successfully found the <see cref="IJsonNavigatorNode"/> with the specified property name; <code>false</code> otherwise.</returns>
        public abstract bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <summary>
        /// Gets an IEnumerable of <see cref="ObjectProperty"/> properties from an object node.
        /// </summary>
        /// <param name="objectNode">The <see cref="IJsonNavigatorNode"/> of object node to get the properties from.</param>
        /// <returns>The IEnumerable of <see cref="ObjectProperty"/> properties from an object node.</returns>
        public abstract IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode);

        /// <summary>
        /// Tries to get the buffered raw json
        /// </summary>
        /// <param name="jsonNode">The json node of interest</param>
        /// <param name="bufferedRawJson">The raw json.</param>
        /// <returns>True if bufferedRawJson was set. False otherwise.</returns>
        public abstract bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out IReadOnlyList<byte> bufferedRawJson);
    }
}
