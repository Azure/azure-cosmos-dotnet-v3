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

        /// <inheritdoc />
        public abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Creates a JsonNavigator that can navigate a supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to navigate</param>
        /// <param name="jsonStringDictionary">The optional json string dictionary for binary encoding.</param>
        /// <param name="skipValidation">Whether validation should be skipped.</param>
        /// <returns>A concrete JsonNavigator that can navigate the supplied buffer.</returns>
        public static IJsonNavigator Create(
            ArraySegment<byte> buffer,
            JsonStringDictionary jsonStringDictionary = null,
            bool skipValidation = false)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            // Examine the first buffer byte to determine the serialization format
            byte firstByte = buffer.AsSpan<byte>()[0];

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

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetRootNode();

        /// <inheritdoc />
        public abstract JsonNodeType GetNodeType(ReadOnlyMemory<byte> node);

        /// <inheritdoc />
        public abstract Number64 GetNumberValue(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedStringValue(
            ReadOnlyMemory<byte> stringNode,
            out ReadOnlyMemory<byte> bufferedStringValue);

        /// <inheritdoc />
        public abstract string GetStringValue(ReadOnlyMemory<byte> stringNode);

        /// <inheritdoc />
        public abstract sbyte GetInt8Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract short GetInt16Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract int GetInt32Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract long GetInt64Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract float GetFloat32Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract double GetFloat64Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract uint GetUInt32Value(ReadOnlyMemory<byte> numberNode);

        /// <inheritdoc />
        public abstract Guid GetGuidValue(ReadOnlyMemory<byte> guidNode);

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetBinaryValue(ReadOnlyMemory<byte> binaryNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedBinaryValue(
            ReadOnlyMemory<byte> binaryNode,
            out ReadOnlyMemory<byte> bufferedBinaryValue);

        /// <inheritdoc />
        public abstract int GetArrayItemCount(ReadOnlyMemory<byte> arrayNode);

        /// <inheritdoc />
        public abstract ReadOnlyMemory<byte> GetArrayItemAt(ReadOnlyMemory<byte> arrayNode, int index);

        /// <inheritdoc />
        public abstract IEnumerable<ReadOnlyMemory<byte>> GetArrayItems(ReadOnlyMemory<byte> arrayNode);

        /// <inheritdoc />
        public abstract int GetObjectPropertyCount(ReadOnlyMemory<byte> objectNode);

        /// <inheritdoc />
        public abstract bool TryGetObjectProperty(ReadOnlyMemory<byte> objectNode, string propertyName, out ObjectProperty objectProperty);

        /// <inheritdoc />
        public abstract IEnumerable<ObjectProperty> GetObjectProperties(ReadOnlyMemory<byte> objectNode);

        /// <inheritdoc />
        public abstract bool TryGetBufferedRawJson(ReadOnlyMemory<byte> jsonNode, out ReadOnlyMemory<byte> bufferedRawJson);
    }
}
