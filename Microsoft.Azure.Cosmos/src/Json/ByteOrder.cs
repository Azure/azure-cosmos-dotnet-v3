//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    /// <summary>
    /// The ByteOrder class is capable of reversing the bytes of any primitive type.
    /// </summary>
    internal static class ByteOrder
    {
        /// <summary>
        /// Reverses a single byte.
        /// </summary>
        /// <param name="value">The byte to reverse</param>
        /// <returns>The reversed byte.</returns>
        /// <remarks>Since a single byte has no byte order, so the value itself is returned so this is essentially a NO-OP.</remarks>
        public static byte Reverse(byte value)
        {
            return value;
        }

        /// <summary>
        /// Reverses a bool.
        /// </summary>
        /// <param name="value">The bool to reverse</param>
        /// <returns>The reversed bool.</returns>
        /// <remarks>Since a bool has no byte order, so the value itself is returned so this is essentially a NO-OP.</remarks>
        public static bool Reverse(bool value)
        {
            return value;
        }

        /// <summary>
        /// Reverses a char.
        /// </summary>
        /// <param name="value">The char to reverse.</param>
        /// <returns>The reversed char.</returns>
        public static char Reverse(char value)
        {
            ushort b1 = (ushort)(((ushort)value >> 0) & 0xff);
            ushort b2 = (ushort)(((ushort)value >> 8) & 0xff);

            return (char)((b1 << 8) | (b2 << 0));
        }

        /// <summary>
        /// Reverses a short.
        /// </summary>
        /// <param name="value">The short to reverse.</param>
        /// <returns>The reversed short.</returns>
        public static short Reverse(short value)
        {
            ushort b1 = (ushort)(((ushort)value >> 0) & 0xff);
            ushort b2 = (ushort)(((ushort)value >> 8) & 0xff);

            return (short)((b1 << 8) | (b2 << 0));
        }

        /// <summary>
        /// Reverses a ushort.
        /// </summary>
        /// <param name="value">The ushort to reverse.</param>
        /// <returns>The reversed ushort.</returns>
        public static ushort Reverse(ushort value)
        {
            ushort b1 = (ushort)((value >> 0) & 0xff);
            ushort b2 = (ushort)((value >> 8) & 0xff);

            return (ushort)((b1 << 8) | (b2 << 0));
        }

        /// <summary>
        /// Reverses a int.
        /// </summary>
        /// <param name="value">The int to reverse.</param>
        /// <returns>The reversed int.</returns>
        public static int Reverse(int value)
        {
            uint b1 = ((uint)value >> 0) & 0xff;
            uint b2 = ((uint)value >> 8) & 0xff;
            uint b3 = ((uint)value >> 16) & 0xff;
            uint b4 = ((uint)value >> 24) & 0xff;

            return (int)((b1 << 24) | (b2 << 16) | (b3 << 8) | (b4 << 0));
        }

        /// <summary>
        /// Reverses a uint.
        /// </summary>
        /// <param name="value">The uint to reverse.</param>
        /// <returns>The reversed uint.</returns>
        public static uint Reverse(uint value)
        {
            uint b1 = (value >> 0) & 0xff;
            uint b2 = (value >> 8) & 0xff;
            uint b3 = (value >> 16) & 0xff;
            uint b4 = (value >> 24) & 0xff;

            return (b1 << 24) | (b2 << 16) | (b3 << 8) | (b4 << 0);
        }

        /// <summary>
        /// Reverses a long.
        /// </summary>
        /// <param name="value">The long to reverse.</param>
        /// <returns>The reversed long.</returns>
        public static long Reverse(long value)
        {
            ulong b1 = ((ulong)value >> 0) & 0xff;
            ulong b2 = ((ulong)value >> 8) & 0xff;
            ulong b3 = ((ulong)value >> 16) & 0xff;
            ulong b4 = ((ulong)value >> 24) & 0xff;
            ulong b5 = ((ulong)value >> 32) & 0xff;
            ulong b6 = ((ulong)value >> 40) & 0xff;
            ulong b7 = ((ulong)value >> 48) & 0xff;
            ulong b8 = ((ulong)value >> 56) & 0xff;

            return (long)((b1 << 56) | (b2 << 48) | (b3 << 40) | (b4 << 32) | (b5 << 24) | (b6 << 16) | (b7 << 8) | (b8 << 0));
        }

        /// <summary>
        /// Reverses a ulong.
        /// </summary>
        /// <param name="value">The ulong to reverse.</param>
        /// <returns>The reversed ulong.</returns>
        public static ulong Reverse(ulong value)
        {
            ulong b1 = (value >> 0) & 0xff;
            ulong b2 = (value >> 8) & 0xff;
            ulong b3 = (value >> 16) & 0xff;
            ulong b4 = (value >> 24) & 0xff;
            ulong b5 = (value >> 32) & 0xff;
            ulong b6 = (value >> 40) & 0xff;
            ulong b7 = (value >> 48) & 0xff;
            ulong b8 = (value >> 56) & 0xff;

            return (b1 << 56) | (b2 << 48) | (b3 << 40) | (b4 << 32) | (b5 << 24) | (b6 << 16) | (b7 << 8) | (b8 << 0);
        }

        /// <summary>
        /// Reverses a float.
        /// </summary>
        /// <param name="value">The float to reverse.</param>
        /// <returns>The reversed float.</returns>
        public static float Reverse(float value)
        {
            byte[] floatAsBytes = BitConverter.GetBytes(value);
            Array.Reverse(floatAsBytes);
            return BitConverter.ToSingle(floatAsBytes, 0);
        }

        /// <summary>
        /// Reverses a double.
        /// </summary>
        /// <param name="value">The double to reverse.</param>
        /// <returns>The reversed double.</returns>
        public static double Reverse(double value)
        {
            byte[] doubleAsBytes = BitConverter.GetBytes(value);
            Array.Reverse(doubleAsBytes);
            return BitConverter.ToDouble(doubleAsBytes, 0);
        }
    }
}
