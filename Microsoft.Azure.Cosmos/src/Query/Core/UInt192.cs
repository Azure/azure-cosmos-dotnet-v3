//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Struct that represents a 192 bit unsigned integer
    /// </summary>
    internal struct UInt192 : IComparable, IComparable<UInt192>, IEquatable<UInt192>
    {
        /// <summary>
        /// Maximum UInt192.
        /// </summary>
        public static readonly UInt192 MaxValue = new UInt192(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

        /// <summary>
        /// Maximum UInt192.
        /// </summary>
        public static readonly UInt192 MinValue = 0;

        /// <summary>
        /// The length of a UInt192 in bytes.
        /// </summary>
        private const int Length = 24;

        /// <summary>
        /// The lowest 64 bits of the UInt192.
        /// </summary>
        private readonly ulong low;

        /// <summary>
        /// The middle 64 bits of the UInt192.
        /// </summary>
        private readonly ulong mid;

        /// <summary>
        /// The highest 64 bits of the UInt192.
        /// </summary>
        private readonly ulong high;

        /// <summary>
        /// Initializes a new instance of the UInt192 struct.
        /// </summary>
        /// <param name="low">The lowest 64 bits of the UInt192.</param>
        /// <param name="mid">The middle 64 bits of the UInt192.</param>
        /// <param name="high">The highest 64 bits of the UInt192.</param>
        private UInt192(ulong low, ulong mid, ulong high)
        {
            this.low = low;
            this.mid = mid;
            this.high = high;
        }

        #region Static Operators
        /// <summary>
        /// Adds two instances of UInt192 together.
        /// </summary>
        /// <param name="augend">The augend.</param>
        /// <param name="addend">The addend.</param>
        /// <returns>The augend + addend.</returns>
        public static UInt192 operator +(UInt192 augend, UInt192 addend)
        {
            ulong low = augend.low + addend.low;
            ulong mid = augend.mid + addend.mid;
            ulong high = augend.high + addend.high;

            if (low < augend.low)
            {
                mid++;
            }

            if (mid < augend.mid)
            {
                high++;
            }

            return new UInt192(low, mid, high);
        }

        /// <summary>
        /// Takes the difference between two UInt192.
        /// </summary>
        /// <param name="minuend">The minuend.</param>
        /// <param name="subtrahend">The subtrahend.</param>
        /// <returns>minuend - subtrahend.</returns>
        public static UInt192 operator -(UInt192 minuend, UInt192 subtrahend)
        {
            ulong low = minuend.low - subtrahend.low;
            ulong mid = minuend.mid - subtrahend.mid;
            ulong high = minuend.high - subtrahend.high;

            if (low > minuend.low)
            {
                mid--;
            }

            if (mid > minuend.mid)
            {
                high--;
            }

            return new UInt192(low, mid, high);
        }

        /// <summary>
        /// Returns if one UInt192 is less than another UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is less than right.</returns>
        public static bool operator <(UInt192 left, UInt192 right)
        {
            return (left.high < right.high)
                || ((left.high == right.high) && (left.mid < right.mid))
                || ((left.mid == right.mid) && (left.low < right.low));
        }

        /// <summary>
        /// Returns if one UInt192 is greater than another UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is greater than right.</returns>
        public static bool operator >(UInt192 left, UInt192 right)
        {
            return right < left;
        }

        /// <summary>
        /// Returns if one UInt192 is less than or equal to another UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is less than or equal to the right.</returns>
        public static bool operator <=(UInt192 left, UInt192 right)
        {
            return !(right < left);
        }

        /// <summary>
        /// Returns if one UInt192 is greater than or equal to another UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether left is greater than or equal to the right.</returns>
        public static bool operator >=(UInt192 left, UInt192 right)
        {
            return !(left < right);
        }

        /// <summary>
        /// Returns if two UInt192 are equal.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether the left is equal to the right.</returns>
        public static bool operator ==(UInt192 left, UInt192 right)
        {
            return (left.high == right.high) && (left.mid == right.mid) && (left.low == right.low);
        }

        /// <summary>
        /// Returns if two UInt192 are not equal.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>Whether the left is not equal to the right.</returns>
        public static bool operator !=(UInt192 left, UInt192 right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Takes the bitwise and of two instance of UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise and of two instance of UInt192..</returns>
        public static UInt192 operator &(UInt192 left, UInt192 right)
        {
            return new UInt192(left.low & right.low, left.mid & right.mid, left.high & right.high);
        }

        /// <summary>
        /// Takes the bitwise or of two instance of UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise or of two instance of UInt192..</returns>
        public static UInt192 operator |(UInt192 left, UInt192 right)
        {
            return new UInt192(left.low | right.low, left.mid | right.mid, left.high | right.high);
        }

        /// <summary>
        /// Takes the bitwise x or of two instance of UInt192.
        /// </summary>
        /// <param name="left">The left hand side of the operator.</param>
        /// <param name="right">The right hand side of the operator.</param>
        /// <returns>The bitwise x or of two instance of UInt192..</returns>
        public static UInt192 operator ^(UInt192 left, UInt192 right)
        {
            return new UInt192(left.low ^ right.low, left.mid ^ right.mid, left.high ^ right.high);
        }

        /// <summary>
        /// Implicitly converts an int to UInt192.
        /// </summary>
        /// <param name="value">The int to convert.</param>
        public static implicit operator UInt192(int value)
        {
            return new UInt192((ulong)value, 0, 0);
        }
        #endregion

        #region Static Implicit Operators
        /// <summary>
        /// Implicitly converts an unsigned int to UInt192.
        /// </summary>
        /// <param name="value">The unsigned int to convert.</param>
        public static implicit operator UInt192(uint value)
        {
            return new UInt192((ulong)value, 0, 0);
        }

        /// <summary>
        /// Implicitly converts an unsigned long to UInt192.
        /// </summary>
        /// <param name="value">The unsigned long to convert.</param>
        public static implicit operator UInt192(ulong value)
        {
            return new UInt192(value, 0, 0);
        }

        /// <summary>
        /// Implicitly converts an long to UInt192.
        /// </summary>
        /// <param name="value">The long to convert.</param>
        public static implicit operator UInt192(long value)
        {
            return new UInt192((ulong)value, 0, 0);
        }

        /// <summary>
        /// Implicitly converts an UInt128 to UInt192.
        /// </summary>
        /// <param name="value">The UInt128 to convert.</param>
        public static implicit operator UInt192(UInt128 value)
        {
            return new UInt192(value.GetLow(), value.GetHigh(), 0);
        }
        #endregion

        /// <summary>
        /// Parses a UInt192 from its ToString() output.
        /// </summary>
        /// <param name="value">The ToString() output.</param>
        /// <returns>The Parsed UInt192.</returns>
        public static UInt192 Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("value can not be null or empty.");
            }
            string[] hexPairs = value.Split('-');
            if (hexPairs.Length != UInt192.Length)
            {
                throw new ArgumentException("not enough bytes encoded.");
            }

            byte[] bytes = new byte[UInt192.Length];
            for (int index = 0; index < UInt192.Length; index++)
            {
                bytes[index] = byte.Parse(hexPairs[index], System.Globalization.NumberStyles.HexNumber, null);
            }

            return UInt192.FromByteArray(bytes);
        }

        /// <summary>
        /// Creates a UInt192 from 3 ulong
        /// </summary>
        /// <param name="low">The lowest 64 bits of the ulong.</param>
        /// <param name="mid">The middle 64 bits of the ulong.</param>
        /// <param name="high">The upper 64 bits of the ulong.</param>
        public static UInt192 Create(ulong low, ulong mid, ulong high)
        {
            return new UInt192(low, mid, high);
        }

        /// <summary>
        /// Creates a UInt192 from a byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="start">The starting index.</param>
        /// <returns>The UInt192 from the byte array.</returns>
        public static UInt192 FromByteArray(byte[] bytes, int start = 0)
        {
            ulong low = BitConverter.ToUInt64(bytes, start);
            ulong mid = BitConverter.ToUInt64(bytes, start + 8);
            ulong high = BitConverter.ToUInt64(bytes, start + 16);

            return new UInt192(low, mid, high);
        }

        /// <summary>
        /// Converts the UInt192 to a byte array.
        /// </summary>
        /// <param name="uint192">The UInt192 to convert.</param>
        /// <returns>The byte array representation of this UInt192.</returns>
        public static byte[] ToByteArray(UInt192 uint192)
        {
            byte[] bytes = new byte[UInt192.Length];
            BitConverter.GetBytes(uint192.low).CopyTo(bytes, 0);
            BitConverter.GetBytes(uint192.mid).CopyTo(bytes, 8);
            BitConverter.GetBytes(uint192.high).CopyTo(bytes, 16);

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

            if (value is UInt192)
            {
                return this.CompareTo((UInt192)value);
            }

            throw new ArgumentException("Value must be a UInt192.");
        }

        /// <summary>
        /// Compares this UInt192 to another instance of the UInt192 type.
        /// </summary>
        /// <param name="other">The other instance to compare to.</param>
        /// <returns>
        /// A negative number if this instance is less than the other instance.
        /// Zero if they are the same.
        /// A positive number if this instance is greater than the other instance.
        /// </returns>
        public int CompareTo(UInt192 other)
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

            if (obj is UInt192)
            {
                return this.Equals((UInt192)obj);
            }

            return false;
        }

        /// <summary>
        /// Returns whether this UInt192 equals another UInt192.
        /// </summary>
        /// <param name="other">The UInt192 to compare to.</param>
        /// <returns>Whether this UInt192 equals another UInt192.</returns>
        public bool Equals(UInt192 other)
        {
            return this == other;
        }

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>The hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return (int)(this.low.GetHashCode() ^ this.mid.GetHashCode() ^ this.high.GetHashCode());
        }

        /// <summary>
        /// Gets the string representation of a UInt192 as a hex dump.
        /// </summary>
        /// <returns>The string representation of a UInt192 as a hex dump.</returns>
        public override string ToString()
        {
            byte[] bytes = UInt192.ToByteArray(this);
            return BitConverter.ToString(bytes);
        }

        /// <summary>
        /// Gets the highest 64 bits of the UInt192.
        /// </summary>
        /// <returns>The highest 64 bits of the UInt192.</returns>
        public ulong GetHigh()
        {
            return this.high;
        }

        /// <summary>
        /// Gets the middle 64 bits of the UInt192.
        /// </summary>
        /// <returns>The middle 64 bits of the UInt192.</returns>
        public ulong GetMid()
        {
            return this.mid;
        }

        /// <summary>
        /// Gets the lowest 64 bits of the UInt192.
        /// </summary>
        /// <returns>The lowest 64 bits of the UInt192.</returns>
        public ulong GetLow()
        {
            return this.low;
        }
    }
}
