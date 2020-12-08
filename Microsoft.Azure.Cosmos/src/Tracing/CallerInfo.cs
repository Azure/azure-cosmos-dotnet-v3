// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;

    /// <summary>
    /// The metadata for who created called the method and the source file path.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif  
        readonly struct CallerInfo
    {
        /// <summary>
        /// Initializes a new instance of the CallerInfo class.
        /// </summary>
        /// <param name="memberName">The name of the file that called the method.</param>
        /// <param name="filePath">The path to the file in source that called the method.</param>
        /// <param name="lineNumber">The line number of the file that called the method.</param>
        public CallerInfo(string memberName, string filePath, int lineNumber)
        {
            this.MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
            this.FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.LineNumber = lineNumber < 0 ? throw new ArgumentOutOfRangeException(nameof(lineNumber)) : lineNumber;
        }

        /// <summary>
        /// Gets the name of the file that called the method.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Gets the path to the file in source that called the method.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the line number of the file that called the method.
        /// </summary>
        public int LineNumber { get; }
    }
}
