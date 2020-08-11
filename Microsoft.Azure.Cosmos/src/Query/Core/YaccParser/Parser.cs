// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed partial class Parser
    {
        private static class Erorrs
        {
            public const string SqlSelectStarWithNoFrom = "Syntax error, 'SELECT *' is not valid if FROM clause is omitted.";
        }

        private readonly Scanner scanner;
        private readonly List<SqlError> errors;
        private readonly Dictionary<ReadOnlyMemory<char>, string> stringCache;

        private Parser(ReadOnlyMemory<char> text)
        {
            this.scanner = new Scanner(text);
            this.errors = new List<SqlError>();
            this.stringCache = new Dictionary<ReadOnlyMemory<char>, string>();
        }

        public SqlParseResult Parse(ReadOnlyMemory<char> text)
        {
            Parser parser = new Parser(text);
            SqlProgram program = parser.Parse();
            SqlParseResult parseResult = new SqlParseResult(text, program, parser.errors);

            return parseResult;
        }

        private SqlProgram Parse()
        {
            SqlProgram program = yyparse(this);
            return program;
        }

        private bool TryGetNextToken(out int tokenId, out YYSTYPE value, out YYLTYPE location)
        {
            throw new NotImplementedException();
        }

        private readonly struct YYSTYPE
        {
            public YYSTYPE(SqlObject queryObject)
            {
                this.QueryObject = queryObject ?? throw new ArgumentNullException(nameof(queryObject));
            }

            public SqlObject QueryObject { get; }

            public static implicit operator SqlObject(YYSTYPE value) => value.QueryObject;
            public static explicit operator YYSTYPE(SqlObject value) => new YYSTYPE(value);
        }

        private readonly struct YYLTYPE
        {
            private readonly SqlLocation location;

            public YYLTYPE(SqlLocation location)
            {
                this.location = location;
            }

            public ulong StartIndex => this.location.StartIndex;
            public ulong EndIndex => this.location.EndIndex;

            public static implicit operator SqlLocation(YYLTYPE value) => value.location;
            public static explicit operator YYLTYPE(SqlLocation value) => new YYLTYPE(value);
        }
    }
}
