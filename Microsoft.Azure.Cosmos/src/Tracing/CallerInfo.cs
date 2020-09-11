// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;

    internal readonly struct CallerInfo
    {
        public CallerInfo(string memberName, string filePath, int lineNumber)
        {
            this.MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
            this.FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.LineNumber = lineNumber < 0 ? throw new ArgumentOutOfRangeException(nameof(lineNumber)) : lineNumber;
        }

        public string MemberName { get; }
        public string FilePath { get; }
        public int LineNumber { get; }
    }
}
