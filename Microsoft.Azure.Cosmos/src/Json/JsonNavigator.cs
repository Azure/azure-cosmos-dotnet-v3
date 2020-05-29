//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Core.Utf8;

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

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Creates a JsonNavigator that can navigate a supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to navigate</param>
        /// <param name="jsonStringDictionary">The optional json string dictionary for binary encoding.</param>
        /// <returns>A concrete JsonNavigator that can navigate the supplied buffer.</returns>
        public static IJsonNavigator Create(
            ReadOnlyMemory<byte> buffer,
            JsonStringDictionary jsonStringDictionary = null)
        {
            if (buffer.IsEmpty)
            {
                throw new ArgumentOutOfRangeException($"{nameof(buffer)} can not be empty.");
            }

            // Examine the first buffer byte to determine the serialization format
            byte firstByte = buffer.Span[0];

            switch ((JsonSerializationFormat)firstByte)
            {
                // Explicitly pick from the set of supported formats
                case JsonSerializationFormat.Binary:
                    return new JsonBinaryNavigator(buffer, jsonStringDictionary);
                default:
                    // or otherwise assume text format
                    return new JsonTextNavigator(buffer);
            }
        }

        /// <inheritdoc />
        public abstract IJsonNavigatorNode GetRootNode();

        /// <inheritdoc />
        public abstract JsonNodeType GetNodeType(IJsonNavigatorNode node);

        /// <inheritdoc />
        public abstract Number64 GetNumber64Value(IJsonNavigatorNode numberNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(IJsonNavigatorNode stringNode, out Utf8Memory bufferedStringValue);

        /// <inheritdoc />
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
        public abstract ReadOnlyMemory<byte> GetBinaryValue(IJsonNavigatorNode binaryNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedBinaryValue(IJsonNavigatorNode binaryNode, out ReadOnlyMemory<byte> bufferedBinaryValue);

        /// <inheritdoc />
        public abstract int GetArrayItemCount(IJsonNavigatorNode arrayNode);

        /// <inheritdoc />
        public abstract IJsonNavigatorNode GetArrayItemAt(IJsonNavigatorNode arrayNode, int index);

        /// <inheritdoc />
        public abstract IEnumerable<IJsonNavigatorNode> GetArrayItems(IJsonNavigatorNode arrayNode);

        /// <inheritdoc />
        public abstract int GetObjectPropertyCount(IJsonNavigatorNode objectNode);

        /// <inheritdoc />
        public abstract bool TryGetObjectProperty(IJsonNavigatorNode objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <inheritdoc />
        public abstract IEnumerable<ObjectProperty> GetObjectProperties(IJsonNavigatorNode objectNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedRawJson(IJsonNavigatorNode jsonNode, out ReadOnlyMemory<byte> bufferedRawJson);

        /// <inheritdoc />
        public abstract T Materialize<T>(Newtonsoft.Json.JsonSerializer jsonSerializer, IJsonNavigatorNode jsonNavigatorNode);
    }
}
