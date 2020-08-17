namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.Text.Encoding;

    public sealed class SqlCharSerializer : SqlSerializer<string>
    {
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;
        private static readonly char Padding = ' ';

        /// <inheritdoc/>
        public override string Identifier => "SQL_Char_Nullable";

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

        public SqlCharSerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        public override string Deserialize(byte[] bytes) => this.PadToLength(ASCII.GetString(bytes));

        public override byte[] Serialize(string value) => ASCII.GetBytes(this.TrimToLength(value));

        private string TrimToLength(string value) => new string(value.Take(this.Size).ToArray());

        private string PadToLength(string value) => value.PadRight(this.Size, Padding);
    }
}
