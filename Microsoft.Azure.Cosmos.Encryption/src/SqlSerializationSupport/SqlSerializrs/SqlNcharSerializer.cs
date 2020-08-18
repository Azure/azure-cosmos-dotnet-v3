using System;
using System.Linq;

using static System.Linq.Enumerable;
using static System.Text.Encoding;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlNcharSerializer : SqlSerializer<string>
    {
        private const int MinSize = 1;
        private const int MaxSize = 4000;
        private const int DefaultSize = 30;
        private static readonly char Padding = ' ';

        /// <inheritdoc/>
        public override string Identifier => "SQL_NChar_Nullable";

        private int size;

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

        public SqlNcharSerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : this.PadToLength(Unicode.GetString(bytes));
        }

        public override byte[] Serialize(string value)
        {
            return value.IsNull() ? null : Unicode.GetBytes(this.TrimToLength(value));
        }

        private string TrimToLength(string value) => new string(value.Take(this.Size).ToArray());

        private string PadToLength(string value) => value.PadRight(this.Size, Padding);
    }
}
