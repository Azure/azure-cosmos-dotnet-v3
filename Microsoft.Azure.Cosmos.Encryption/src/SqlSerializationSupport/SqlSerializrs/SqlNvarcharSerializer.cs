// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.Linq.Enumerable;
    using static System.Text.Encoding;

    /// <summary>
    /// Sql string Seriaizer.
    /// </summary>
    public sealed class SqlNvarcharSerializer : SqlSerializer<string>
    {
        private const int Max = -1;
        private const int MinSize = 1;
        private const int MaxSize = 4000;
        private const int DefaultSize = 30;

        /// <inheritdoc/>
        public override string Identifier => "SQL_NVarChar_Nullable";

        private int size;

        /// <summary>
        ///  Gets or sets string Size to be set.
        /// </summary>
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

        /// <summary>
        /// Sql char Serializer
        /// </summary>
        /// <param name="size"> size set to default 30 </param>
        public SqlNvarcharSerializer(int size = DefaultSize)
        {
            this.Size = size;
        }

        /// <summary>
        /// Deserialize to string value
        /// </summary>
        /// <param name="bytes"> bytes to deserialize </param>
        /// <returns> string </returns>
        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : Unicode.GetString(bytes);
        }

        /// <summary>
        /// Serialize string value
        /// </summary>
        /// <param name="value"> value </param>
        /// <returns> byte[] </returns>
        public override byte[] Serialize(string value)
        {
            if (value.IsNull())
            {
                return null;
            }

            if (this.size != Max)
            {
                string trimmedValue = this.TrimToLength(value);
                return Unicode.GetBytes(trimmedValue);
            }

            return Unicode.GetBytes(value);
        }

        private string TrimToLength(string value) => new string(value.Take(this.Size).ToArray());
    }
}
