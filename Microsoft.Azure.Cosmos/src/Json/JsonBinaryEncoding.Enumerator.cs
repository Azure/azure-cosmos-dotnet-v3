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
            public static IEnumerable<ArrayItem> GetArrayItems(
                ReadOnlyMemory<byte> rootBuffer,
                int arrayOffset,
                UniformArrayInfo externalArrayInfo)
            {
                ReadOnlyMemory<byte> buffer = rootBuffer.Slice(arrayOffset);
                byte typeMarker = buffer.Span[0];

                UniformArrayInfo uniformArrayInfo;
                if (externalArrayInfo != null)
                {
                    uniformArrayInfo = externalArrayInfo.NestedArrayInfo;
                }
                else
                {
                    uniformArrayInfo = IsUniformArrayTypeMarker(typeMarker) ? GetUniformArrayInfo(buffer.Span) : null;
                }

                if (uniformArrayInfo != null)
                {
                    int itemStartOffset = arrayOffset + uniformArrayInfo.PrefixSize;
                    int itemEndOffset = itemStartOffset + (uniformArrayInfo.ItemSize * uniformArrayInfo.ItemCount);
                    for (int offset = itemStartOffset; offset < itemEndOffset; offset += uniformArrayInfo.ItemSize)
                    {
                        yield return new ArrayItem(offset, uniformArrayInfo);
                    }
                }
                else
                {
                    if (!TypeMarker.IsArray(typeMarker))
                    {
                        throw new JsonInvalidTokenException();
                    }

                    int firstArrayItemOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                    int arrayLength = JsonBinaryEncoding.GetValueLength(buffer.Span);

                    // Scope to just the array
                    buffer = buffer.Slice(0, arrayLength);

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

                        yield return new ArrayItem(arrayOffset + (arrayLength - buffer.Length), null);

                        // Slice off the array item
                        buffer = buffer.Slice(arrayItemLength);
                    }
                }
            }

            public static IEnumerable<ObjectProperty> GetObjectProperties(
                ReadOnlyMemory<byte> rootBuffer,
                int objectOffset)
            {
                ReadOnlyMemory<byte> buffer = rootBuffer.Slice(objectOffset);
                byte typeMarker = buffer.Span[0];

                if (!JsonBinaryEncoding.TypeMarker.IsObject(typeMarker))
                {
                    throw new JsonInvalidTokenException();
                }

                int firstValueOffset = JsonBinaryEncoding.GetFirstValueOffset(typeMarker);
                int objectLength = JsonBinaryEncoding.GetValueLength(buffer.Span);

                // Scope to just the array
                buffer = buffer.Slice(0, objectLength);

                // Seek to the first object property
                buffer = buffer.Slice(firstValueOffset);
                while (buffer.Length != 0)
                {
                    int nameNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (nameNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }

                    int nameOffset = objectOffset + (objectLength - buffer.Length);

                    buffer = buffer.Slice(nameNodeLength);

                    int valueNodeLength = JsonBinaryEncoding.GetValueLength(buffer.Span);
                    if (valueNodeLength > buffer.Length)
                    {
                        throw new JsonInvalidTokenException();
                    }

                    int valueOffset = objectOffset + (objectLength - buffer.Length);

                    buffer = buffer.Slice(valueNodeLength);

                    yield return new ObjectProperty(nameOffset, valueOffset);
                }
            }

            public readonly struct ArrayItem
            {
                public ArrayItem(int offset, UniformArrayInfo externalArrayInfo)
                {
                    this.Offset = offset;
                    this.ExternalArrayInfo = externalArrayInfo;
                }

                public int Offset { get; }
                public UniformArrayInfo ExternalArrayInfo { get; }
            }

            public readonly struct ObjectProperty
            {
                public ObjectProperty(int nameOffset, int valueOffset)
                {
                    this.NameOffset = nameOffset;
                    this.ValueOffset = valueOffset;
                }

                public int NameOffset { get; }
                public int ValueOffset { get; }
            }
        }
    }
}
