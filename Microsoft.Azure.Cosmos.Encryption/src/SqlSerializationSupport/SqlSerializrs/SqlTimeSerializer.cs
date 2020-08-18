using System;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Azure.Cosmos.Encryption
{
    public sealed class SqlTimeSerializer : SqlSerializer<TimeSpan>
    {
        private const int MaxScale = 7;
        private const int MinScale = 0;
        private const int DefaultScale = 7;
        private const int MaxTimeLength = 5;
        private static readonly TimeSpan MinValue = TimeSpan.Parse("00:00:00.0000000");
        private static readonly TimeSpan MaxValue = TimeSpan.Parse("23:59:59.9999999");

        /// <inheritdoc/>
        public override string Identifier => "SQL_Time";

        private int scale;

        public int Scale
        {
            get => this.scale;
            set
            {
                if (value < MinScale || value > MaxScale)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }
                this.scale = value;
            }
        }

        public SqlTimeSerializer(int scale = DefaultScale)
        {
            this.Scale = scale;
        }

        public override TimeSpan Deserialize(byte[] bytes)
        {
            byte[] padding = { 0, 0, 0 };
            byte[] paddedBytes = bytes.Concat(padding).ToArray();
            long timeTicks = ToInt64(paddedBytes, 0);

            return new TimeSpan(timeTicks);
        }

        public override byte[] Serialize(TimeSpan value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            long time = value.Ticks / precisionScale[this.scale] * precisionScale[this.scale];

            return GetBytes(time).Take(MaxTimeLength).ToArray();
        }

        internal static readonly long[] precisionScale = {
            10000000,
            1000000,
            100000,
            10000,
            1000,
            100,
            10,
            1,
        };
    }
}
