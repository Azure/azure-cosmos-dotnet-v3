// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.Text.Encoding;

    public sealed class SqlVarcharSerializer : SqlSerializer<string>
    {
        private const int Max = -1;
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;

        /// <inheritdoc/>
        public override string Identifier => "SQL_VarChar_Nullable";

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

        public SqlVarcharSerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : ASCII.GetString(bytes);
        }

        public override byte[] Serialize(string value)
        {
            if (value.IsNull())
            {
                return null;
            }

            if (this.Size != Max && value.Length > this.Size)
            {
                value = this.TrimToLength(value);
            }

            return ASCII.GetBytes(value);
        }

        private string TrimToLength(string value) => new string(value.Take(this.Size).ToArray());
    }
}
