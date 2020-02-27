// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Struct that represents a 128 bit unsigned integer.
    /// </summary>
    internal struct UInt128 : IComparable, IComparable<UInt128>, IEquatable<UInt128>
    {
        /// <summary>
        /// Maximum UInt128.
        /// </summary>
        public static readonly UInt128 MaxValue = new UInt128(ulong.MaxValue, ulong.MaxValue);

        /// <summary>
        /// Maximum UInt128.
        /// </summary>
        public static readonly UInt128 MinValue = 0;

        /// <summary>
        /// The length of this struct in bytes.
        /// </summary>
        private const int Length = 16;

        /// <summary>
        /// The lowest 64 bits of the UInt128.
        /// </summary>
        private readonly ulong low;

        /// <summary>
        /// The highest 64 bits of the UInt128.
        /// </summary>
        private readonly ulong high;

        /// <summary>
        /// Initializes a new instance of the <see cref="UInt128"/> struct.
        /// </summary>
        /// <param name="low">The lowest 64 bits of the UInt128.</param>
        /// <param name="high">The highest 64 bits of the UInt128.</param>
        private UInt128(ulong low, ulong high)
        {
            this.low = low;
            this.high = high;
        }

        /// <summary>
        /// Implicitly converts an int to UInt128.
        /// </summary>
        /// <param name="value">The int to convert.</param>
        public static implicit operator UInt128(int value)
        {
            return new UInt128((ulong)value, 0);
        }

        /// <summary>
        /// Implicitly converts a long to UInt128.
        /// </summary>
        /// <param name="value">The int to convert.</param>
        public static implicit operator UInt128(long value)
        {
            return new UInt128((ulong)value, 0);
        }

        /// <summary>
        /// Implicitly converts an unsigned int to UInt128.
        /// </summary>
        /// <param name="value">The unsigned int to convert.</param>
        public static implicit operator UInt128(uint value)
        {
            return new UInt128((ulong)value, 0);
        }

        /// <summary>
        /// Implicitly converts an unsigned long to UInt128.
        /// </summary>
        /// <param name="value">The unsigned long to convert.</param>
        public static implicit operator UInt128(ulong value)
        {
            return new UInt128(value, 0);
        }

        /// <summary>
        /// Adds two instances of UInt128 together.
        /// </summary>
        /// <param name="augend">The augend.</param>
        /// <param name="addend">The addend.</param>
        /// <returns>The augend + addend.</returns>
        public static UInt128 operator +(UInt128 augend, UInt128 addend)
        {
            ulong low = augend.low + addend.low;
            ulong high = augend.high + addend.high;

            if (low < augend.low)
            {
                high++;
            }

            return new UInt128(low, high);
        }

        /// <summary>
        /// Takes the difference between two UInt128.
        /// </summary>
        /// <param name="minuend">The minuend.</param>
        /// <param name="subtrahend">The subtrahend.</param>
        /// <returns>minuend - subtrahend.</returns>
        public static UInt128 operator -(UInt128 minuend, UInt128 subtrahend)
        {
            ulong low = minuend.low - subtrahend.low;
            ulong high = minuend.high - subtrahend.high;

            if (low > minuend.low)
            {
                high--;
            }

            return new UInt128(low, high);
        }

        /// <summary>
        /// Returns if one UInt128 is less than another UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is less than right.</returns>
        public static bool operator <(UInt128 left, UInt128 right)
        {
            return (left.high < right.high)
                || ((left.high == right.high) && (left.low < right.low));
        }

        /// <summary>
        /// Returns if one UInt128 is greater than another UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is greater than right.</returns>
        public static bool operator >(UInt128 left, UInt128 right)
        {
            return right < left;
        }

        /// <summary>
        /// Returns if one UInt128 is less than or equal to another UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is less than or equal to the right.</returns>
        public static bool operator <=(UInt128 left, UInt128 right)
        {
            return !(right < left);
        }

        /// <summary>
        /// Returns if one UInt128 is greater than or equal to another UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is greater than or equal to the right.</returns>
        public static bool operator >=(UInt128 left, UInt128 right)
        {
            return !(left < right);
        }

        /// <summary>
        /// Returns if two UInt128 are equal.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether the left is equal to the right.</returns>
        public static bool operator ==(UInt128 left, UInt128 right)
        {
            return (left.high == right.high) && (left.low == right.low);
        }

        /// <summary>
        /// Returns if two UInt128 are not equal.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether the left is not equal to the right.</returns>
        public static bool operator !=(UInt128 left, UInt128 right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Takes the bitwise and of two instance of UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise and of two instance of UInt128..</returns>
        public static UInt128 operator &(UInt128 left, UInt128 right)
        {
            return new UInt128(left.low & right.low, left.high & right.high);
        }

        /// <summary>
        /// Takes the bitwise or of two instance of UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise or of two instance of UInt128..</returns>
        public static UInt128 operator |(UInt128 left, UInt128 right)
        {
            return new UInt128(left.low | right.low, left.high | right.high);
        }

        /// <summary>
        /// Takes the bitwise x or of two instance of UInt128.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise x or of two instance of UInt128..</returns>
        public static UInt128 operator ^(UInt128 left, UInt128 right)
        {
            return new UInt128(left.low ^ right.low, left.high ^ right.high);
        }

        /// <summary>
        /// Creates a UInt128 from two ulong.
        /// </summary>
        /// <param name="low">The lower 64 bits of the UInt128.</param>
        /// <param name="high">The upper 64 bits of the UInt128.</param>
        /// <returns>A UInt128 from the two ulong.</returns>
        public static UInt128 Create(ulong low, ulong high)
        {
            return new UInt128(low, high);
        }

        public static UInt128 FromByteArray(ReadOnlySpan<byte> buffer)
        {
            if (!UInt128.TryCreateFromByteArray(buffer, out UInt128 value))
            {
                throw new FormatException($"Malformed buffer");
            }

            return value;
        }

        /// <summary>
        /// Converts the UInt128 to a byte array.
        /// </summary>
        /// <param name="uint128">The UInt128 to convert.</param>
        /// <returns>The byte array representation of this UInt128.</returns>
        public static byte[] ToByteArray(UInt128 uint128)
        {
            byte[] bytes = new byte[UInt128.Length];
            byte[] lowBytes = BitConverter.GetBytes(uint128.low);
            byte[] highBytes = BitConverter.GetBytes(uint128.high);
            lowBytes.CopyTo(bytes, 0);
            highBytes.CopyTo(bytes, 8);

            return bytes;
        }

        /// <summary>
        /// Compares this value to an object.
        /// </summary>
        /// <param name="value">The value to compare to.</param>
        /// <returns>The comparison.</returns>
        public int CompareTo(object value)
        {
            if (value == null)
            {
                return 1;
            }

            if (value is UInt128)
            {
                return this.CompareTo((UInt128)value);
            }

            throw new ArgumentException("Value must be a UInt128.");
        }

        /// <summary>
        /// Compares this UInt128 to another instance of the UInt128 type.
        /// </summary>
        /// <param name="other">The other instance to compare to.</param>
        /// <returns>
        /// A negative number if this instance is less than the other instance.
        /// Zero if they are the same.
        /// A positive number if this instance is greater than the other instance.
        /// </returns>
        public int CompareTo(UInt128 other)
        {
            if (this < other)
            {
                return -1;
            }

            if (this > other)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Returns whether this instance equals another object.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>Whether this instance equals another object.</returns>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is UInt128)
            {
                return this.Equals((UInt128)obj);
            }

            return false;
        }

        /// <summary>
        /// Returns whether this UInt128 equals another UInt128.
        /// </summary>
        /// <param name="other">The UInt128 to compare to.</param>
        /// <returns>Whether this UInt128 equals another UInt128.</returns>
        public bool Equals(UInt128 other)
        {
            return this == other;
        }

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>The hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return (int)(this.low.GetHashCode() ^ this.high.GetHashCode());
        }

        /// <summary>
        /// Gets the string representation of a UInt128 as a hex dump.
        /// </summary>
        /// <returns>The string representation of a UInt128 as a hex dump.</returns>
        public override string ToString()
        {
            byte[] bytes = UInt128.ToByteArray(this);
            return BitConverter.ToString(bytes);
        }

        /// <summary>
        /// Returns the high 64 bits of the UInt128.cs.
        /// </summary>
        /// <returns>The high 64 bits of the UInt128.cs.</returns>
        public ulong GetHigh()
        {
            return this.high;
        }

        /// <summary>
        /// Returns the low 64 bits of the UInt128.cs.
        /// </summary>
        /// <returns>The low 64 bits of the UInt128.cs.</returns>
        public ulong GetLow()
        {
            return this.low;
        }

        public static bool TryParse(string value, out UInt128 uInt128)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("value can not be null or empty.");
            }

            string[] hexPairs = value.Split('-').Take(UInt128.Length).ToArray();
            if (hexPairs.Length != UInt128.Length)
            {
                uInt128 = default;
                return false;
            }

            byte[] bytes = new byte[UInt128.Length];
            for (int index = 0; index < UInt128.Length; index++)
            {
                if (!byte.TryParse(hexPairs[index], System.Globalization.NumberStyles.HexNumber, null, out byte parsedBytes))
                {
                    uInt128 = default;
                    return false;
                }

                bytes[index] = parsedBytes;
            }

            uInt128 = UInt128.FromByteArray(bytes);
            return true;
        }

        public static bool TryCreateFromByteArray(ReadOnlySpan<byte> buffer, out UInt128 value)
        {
            if (buffer.Length < UInt128.Length)
            {
                value = default;
                return false;
            }

            ReadOnlySpan<ulong> bufferAsULongs = MemoryMarshal.Cast<byte, ulong>(buffer);
            value = new UInt128(low: bufferAsULongs[0], high: bufferAsULongs[1]);
            return true;
        }
    }
}
