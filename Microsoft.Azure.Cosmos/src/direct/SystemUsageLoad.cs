//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Globalization;
    using System.Text;

    internal struct SystemUsageLoad
    {
        public readonly DateTime Timestamp;
        public readonly float? CpuUsage;
        public readonly long? MemoryAvailable;
        public readonly ThreadInformation ThreadInfo;
        public readonly int? NumberOfOpenTcpConnections;

        public SystemUsageLoad(DateTime timestamp, 
                               ThreadInformation threadInfo,
                               float? cpuUsage = null, 
                               long? memoryAvailable = null,
                               int? numberOfOpenTcpConnection = 0)
        {
            this.Timestamp = timestamp;
            this.CpuUsage = cpuUsage;
            this.MemoryAvailable = memoryAvailable;
            this.ThreadInfo = threadInfo ?? throw new ArgumentNullException("Thread Information can not be null");
            this.NumberOfOpenTcpConnections = numberOfOpenTcpConnection;
        }

        public void AppendJsonString(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"dateUtc\":\"");
            stringBuilder.Append(this.Timestamp.ToString("O"));
            stringBuilder.Append("\",\"cpu\":");
            stringBuilder.Append(this.CpuUsage.HasValue ? this.CpuUsage.Value.ToString("F3") : "\"no info\"");
            stringBuilder.Append(",\"memory\":");
            stringBuilder.Append(this.MemoryAvailable.HasValue ? this.MemoryAvailable.Value.ToString("F3") : "\"no info\"");
            stringBuilder.Append(",\"threadInfo\":");
            if(this.ThreadInfo != null)
            {
                this.ThreadInfo.AppendJsonString(stringBuilder);
            }
            else
            {
                stringBuilder.Append("\"no info\"");
            }
            stringBuilder.Append(",\"numberOfOpenTcpConnection\":");
            stringBuilder.Append(this.NumberOfOpenTcpConnections.HasValue ? this.NumberOfOpenTcpConnections.Value.ToString(CultureInfo.InvariantCulture) : "\"no info\"");

            stringBuilder.Append("}");
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "({0:O} => CpuUsage :{1:F3}, MemoryAvailable :{2:F3} {3:F3}, NumberOfOpenTcpConnection : {4} )",
                this.Timestamp, this.CpuUsage, this.MemoryAvailable, this.ThreadInfo.ToString(), this.NumberOfOpenTcpConnections);
        }
    }
}