// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;

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

            public static IEnumerable<Memory<byte>> GetMutableArrayItems(Memory<byte> buffer)
            {
                foreach (ReadOnlyMemory<byte> readOnlyArrayItem in Enumerator.GetArrayItems(buffer))
                {
                    if (!MemoryMarshal.TryGetArray(readOnlyArrayItem, out ArraySegment<byte> segment))
                    {
                        throw new InvalidOperationException("failed to get array segment.");
                    }

                    yield return buffer.Slice(segment.Offset, length: segment.Count);
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

            public static IEnumerable<MutableObjectProperty> GetMutableObjectProperties(Memory<byte> buffer)
            {
                foreach (ObjectProperty objectProperty in GetObjectProperties(buffer))
                {
                    if (!MemoryMarshal.TryGetArray(objectProperty.Name, out ArraySegment<byte> nameSegment))
                    {
                        throw new InvalidOperationException("failed to get array segment.");
                    }

                    if (!MemoryMarshal.TryGetArray(objectProperty.Value, out ArraySegment<byte> valueSegment))
                    {
                        throw new InvalidOperationException("failed to get array segment.");
                    }

                    Memory<byte> mutableName = buffer.Slice(start: nameSegment.Offset, length: nameSegment.Count);
                    Memory<byte> mutableValue = buffer.Slice(start: valueSegment.Offset, length: valueSegment.Count);

                    yield return new MutableObjectProperty(mutableName, mutableValue);
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

            public readonly struct MutableObjectProperty
            {
                public MutableObjectProperty(
                    Memory<byte> name,
                    Memory<byte> value)
                {
                    this.Name = name;
                    this.Value = value;
                }

                public Memory<byte> Name { get; }
                public Memory<byte> Value { get; }
            }
        }
    }
}
