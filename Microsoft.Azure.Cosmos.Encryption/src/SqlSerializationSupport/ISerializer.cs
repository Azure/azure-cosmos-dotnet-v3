//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    /// <summary>
    /// Contains the methods for serializing and deserializing data objects.
    /// </summary>
    public interface ISerializer
    {
        string Identifier { get; }

        byte[] Serialize(object value);

        object Deserialize(byte[] bytes);
    }


    /// <inheritdoc/>
    /// <typeparam name="T">The type on which this will perform serialization operations.</typeparam>
    public abstract class Serializer<T> : ISerializer
    {
        public abstract string Identifier { get; }

        /// <summary>
        /// Serializes the provided <paramref name="value"/>
        /// </summary>
        /// <param name="value">The value to be serialized</param>
        /// <returns>the serialized data as a byte array</returns>
        public abstract byte[] Serialize(T value);

        /// <summary>
        /// Deserializes the provided <paramref name="bytes"/>
        /// </summary>
        /// <param name="bytes">The data to be deserialized</param>
        /// <returns>The serialized data</returns>
        public abstract T Deserialize(byte[] bytes);

        byte[] ISerializer.Serialize(object value) => this.Serialize((T)value);

        object ISerializer.Deserialize(byte[] bytes) => this.Deserialize(bytes);
    }
}