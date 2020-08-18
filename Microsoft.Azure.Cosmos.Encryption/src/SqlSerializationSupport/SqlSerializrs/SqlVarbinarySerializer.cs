namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.Linq.Enumerable;

    public sealed class SqlVarbinarySerializer : SqlSerializer<byte[]>
    {
        private const int Max = -1;
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;

        /// <inheritdoc/>
        public override string Identifier => "SQL_VarBinary";

        private int size;

        public int Size
        { 
            get => this.size;
            set
            {
                if (value != Max && (value < MinSize || value > MaxSize))
                { 
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range"); 
                }

                this.size = value;
            }
        }

        public SqlVarbinarySerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        public override byte[] Deserialize(byte[] bytes) => bytes;

        public override byte[] Serialize(byte[] value)
        {
            if (value.IsNull())
            {
                return null;
            }

            if (this.size != Max)
            {
                return this.TrimToLength(value);
            }

            return value;
        }

        private byte[] TrimToLength(byte[] value) => value.Take(this.Size).ToArray();
    }
}
