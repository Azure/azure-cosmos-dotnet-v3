namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.Linq.Enumerable;
    /// <inheritdoc/>
    public sealed class SqlBinarySerializer : SqlSerializer<byte[]>
    {
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;
        private static readonly byte Padding = 0;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Binary_Nullable";

        private int size;

        /// <summary>
        /// Gets or sets the maximum length of the data.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the size is defaulted to 30.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 8000] for this setting.
        /// </exception>
        public int Size
        {
            get => this.size;
            set
            {
                if (value < MinSize || value > MaxSize)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }

                this.size = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlBinarySerializer"/> class.
        /// </summary>
        /// <param name="size">The maximum length of the data</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 8000] for this setting.
        /// </exception>
        public SqlBinarySerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <remarks>
        /// If the deserialized value is less than the size, then the value will be padded on the
        /// left to match the size. Padding is achieved by using hexadecimal zeros.
        /// </remarks>
        public override byte[] Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : this.PadToLength(bytes);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is null.
        /// </exception>
        /// <remarks>
        /// If the value is greater than the size, then the value will be truncated on the left to match the size.
        /// </remarks>
        public override byte[] Serialize(byte[] value)
        {
            return value.IsNull() ? null : this.TrimToLength(value);
        }

        private byte[] TrimToLength(byte[] value) => value.Take(this.Size).ToArray();

        private byte[] PadToLength(byte[] value) => value.Concat(Repeat(Padding, this.Size - value.Length)).ToArray();
    }
}
