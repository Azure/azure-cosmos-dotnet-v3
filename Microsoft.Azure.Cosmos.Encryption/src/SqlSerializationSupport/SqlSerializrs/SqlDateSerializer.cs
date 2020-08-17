namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using static System.BitConverter;

    public sealed class SqlDateSerializer : SqlSerializer<DateTime>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Date";

        public override DateTime Deserialize(byte[] bytes)
        {
            byte[] padding = { 0 };
            byte[] bytesWithPadding = bytes.Concat(padding).ToArray();
            int days = ToInt32(bytesWithPadding, 0);
            return DateTime.MinValue.AddDays(days);
        }

        public override byte[] Serialize(DateTime value)
        {
            int days = value.Subtract(DateTime.MinValue).Days;
            return GetBytes(days).Take(3).ToArray();
        }
    }
}
