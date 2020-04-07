//-----------------------------------------------------------------------
// <copyright file="JsonExecutionTimes.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;

    internal readonly struct JsonExecutionTimes
    {
        public JsonExecutionTimes(
            TimeSpan readTime, 
            TimeSpan writeTime, 
            TimeSpan navigationTime, 
            long documentSize, 
            string serializationFormat)
        {
            this.ReadTime = readTime;
            this.WriteTime = writeTime;
            this.NavigationTime = navigationTime;
            this.DocumentSize = documentSize;
            this.SerializationFormat = serializationFormat;
        }

        public TimeSpan ReadTime { get; }

        public TimeSpan WriteTime { get; }

        public TimeSpan NavigationTime { get; }

        public long DocumentSize { get; }

        public string SerializationFormat { get; }
    }
}
