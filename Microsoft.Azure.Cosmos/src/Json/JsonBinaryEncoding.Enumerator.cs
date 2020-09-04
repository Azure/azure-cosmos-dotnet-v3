// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;

    internal static partial class JsonBinaryEncoding
    {
        public static class Enumerator
        {
            public static IEnumerable<ReadOnlyMemory<byte>> GetArrayItems(ReadOnlyMemory<byte> buffer)
            {
                byte typeMarker = buffer.Span[0];
                if (!JsonBinaryEncoding.TypeMarker.IsArray(typeMarker))
                {
                    throw new JsonInvalidTokenException();
                }

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
                    ReadOnlyMemory<byte> arrayItem = buffer.Slice(0, arrayItemLength);
                    yield return arrayItem;

                    // Slice off the array item
                    buffer = buffer.Slice(arrayItemLength);
                }
            }

            public static IEnumerable<ObjectProperty> GetObjectProperties(ReadOnlyMemory<byte> buffer)
            {
                byte typeMarker = buffer.Span[0];
                if (!JsonBinaryEncoding.TypeMarker.IsObject(typeMarker))
                {
                    throw new JsonInvalidTokenException();
                }

                int firstValueOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);

                buffer = buffer.Slice(firstValueOffset);
                while (buffer.Length != 0)
                {
                    int nameNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (nameNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }
                    ReadOnlyMemory<byte> name = buffer.Slice(0, nameNodeLength);
                    buffer = buffer.Slice(nameNodeLength);

                    int valueNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (valueNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }
                    ReadOnlyMemory<byte> value = buffer.Slice(0, valueNodeLength);
                    buffer = buffer.Slice(valueNodeLength);

                    yield return new ObjectProperty(name, value);
                }
            }

            public readonly struct ObjectProperty
            {
                public ObjectProperty(
                    ReadOnlyMemory<byte> name,
                    ReadOnlyMemory<byte> value)
                {
                    this.Name = name;
                    this.Value = value;
                }

                public ReadOnlyMemory<byte> Name { get; }
                public ReadOnlyMemory<byte> Value { get; }
            }
        }
    }
}
