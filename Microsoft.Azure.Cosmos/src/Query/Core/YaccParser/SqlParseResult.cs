// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed class SqlParseResult
    {
        public SqlParseResult(ReadOnlyMemory<char> inputQuery, SqlProgram program, IReadOnlyList<SqlError> errors)
        {
            this.InputQuery = inputQuery;
            this.Program = program;
            this.Errors = errors;

            if ((program == null) && (errors.Count == 0))
            {
                throw new ArgumentException("program must be valid if we have no errors");
            }
        }

        public ReadOnlyMemory<char> InputQuery { get; }
        public SqlProgram Program { get; }
        public IReadOnlyList<SqlError> Errors { get; }

        public bool HasErrors => this.Errors.Count != 0;
    }
}
