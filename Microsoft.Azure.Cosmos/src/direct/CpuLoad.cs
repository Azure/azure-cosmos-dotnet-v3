//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Globalization;

    internal struct CpuLoad
    {
        public DateTime Timestamp;
        public float Value;

        public CpuLoad(DateTime timestamp, float value)
        {
            if ((value < 0.0) || (value > 100.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value,
                    "Valid CPU load values must be between 0.0 and 100.0");
            }
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