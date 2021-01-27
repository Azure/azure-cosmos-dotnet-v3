//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Rntbd
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// A no-allocation replacement for BinaryWriter
    /// that supports Span and Memory.
    /// </summary>
    internal ref struct BytesSerializer
    {
        private readonly Span<byte> targetByteArray;
        private int position;

        public BytesSerializer(byte[] targetByteArray, int length)
        {
            this.targetByteArray = new Span<byte>(targetByteArray, 0, length);
            this.position = 0;
        }

        public static Guid ReadGuidFromBytes(ArraySegment<byte> array)
        {
            Span<byte> activityIdSpan = new Span<byte>(array.Array, array.Offset, array.Count);
            Guid guid = MemoryMarshal.Read<Guid>(activityIdSpan);
            return guid;
        }

        public static unsafe string GetStringFromBytes(ReadOnlyMemory<byte> memory)
        {
            if (memory.IsEmpty)
            {
                return string.Empty;
            }

            fixed (byte* bytes = &memory.Span.GetPinnableReference())
            {
                return Encoding.UTF8.GetString(bytes, memory.Length);
            }
        }

        public static ReadOnlyMemory<byte> GetBytesForString(string toConvert, RntbdConstants.Request request)
        {
            byte[] stringBuffer = request.GetBytes(Encoding.UTF8.GetMaxByteCount(toConvert.Length));
            int length = Encoding.UTF8.GetBytes(toConvert, 0, toConvert.Length, stringBuffer, 0);
            return new ReadOnlyMemory<byte>(stringBuffer, 0, length);
        }

        /// <remarks>
        /// Separate out getting the size of GUID into a separate method to keep unsafe contexts isolated.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int GetSizeOfGuid()
        {
            return sizeof(Guid);
        }

        public void Write(byte[] value)
        {
            this.Write(new ArraySegment<byte>(value, 0, value.Length));
        }

        public void Write(uint value)
        {
            this.WriteValue(value, sizeof(uint));
        }

        public void Write(int value)
        {
            this.WriteValue(value, sizeof(int));
        }

        public void Write(long value)
        {
            this.WriteValue(value, sizeof(long));
        }

        public void Write(ulong value)
        {
            this.WriteValue(value, sizeof(ulong));
        }

        public void Write(float value)
        {
            this.WriteValue(value, sizeof(float));
        }

        public void Write(double value)
        {
            this.WriteValue(value, sizeof(double));
        }

        public void Write(ushort value)
        {
            this.WriteValue(value, sizeof(ushort));
        }

        public void Write(byte value)
        {
            this.WriteValue(value, sizeof(byte));
        }

        public int Write(Guid value)
        {
            this.WriteValue(value, BytesSerializer.GetSizeOfGuid());
            return BytesSerializer.GetSizeOfGuid();
        }

        public void Write(ArraySegment<byte> value)
        {
            Span<byte> valueToWrite = new Span<byte>(value.Array, value.Offset, value.Count);
            this.Write(valueToWrite);
        }

        public void Write(ReadOnlyMemory<byte> valueToWrite)
        {
            this.Write(valueToWrite.Span);
        }

        public void Write(ReadOnlySpan<byte> valueToWrite)
        {
            Span<byte> writeSpan = this.targetByteArray.Slice(this.position);
            valueToWrite.CopyTo(writeSpan);
            this.position += valueToWrite.Length;
        }

        private void WriteValue<T>(T value, int sizeT)
            where T : struct
        {
            Span<byte> writeSpan = this.targetByteArray.Slice(this.position);
            MemoryMarshal.Write(writeSpan, ref value);
            this.position += sizeT;
        }
    }
}
