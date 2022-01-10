//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Globalization;

    internal struct MemoryLoad
    {
        public readonly DateTime Timestamp;
        public readonly long Value;

        public MemoryLoad(DateTime timestamp, long value)
        {
            this.Timestamp = timestamp;
            this.Value = value;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "({0:O} {1:F3})",
                this.Timestamp, this.Value);
        }
    }
}