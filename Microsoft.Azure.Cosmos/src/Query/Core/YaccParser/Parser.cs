// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed class Parser
    {
        private readonly Scanner scanner;
        private readonly List<SqlError> errors;
        private readonly Dictionary<ReadOnlyMemory<char>, string> stringCache;

        private Parser(ReadOnlyMemory<char> text)
        {
            this.scanner = new Scanner(text);
            this.errors = new List<SqlError>();
            this.stringCache = new Dictionary<ReadOnlyMemory<char>, string>();
        }

        private readonly struct YYSTYPE
        {
            private readonly SqlObject queryObject;

            public YYSTYPE(SqlObject queryObject)
            {
                this.queryObject = queryObject ?? throw new ArgumentNullException(nameof(queryObject));
            }

            public static implicit operator SqlObject(YYSTYPE value) => value.queryObject;
            public static explicit operator YYSTYPE(SqlObject value) => new YYSTYPE(value);
        }

        private readonly struct YYLTYPE
        {
            private readonly SqlLocation location;

            public YYLTYPE(SqlLocation location)
            {
                this.location = location;
            }

            public static implicit operator SqlLocation(YYLTYPE value) => value.location;
            public static explicit operator YYLTYPE(SqlLocation value) => new YYLTYPE(value);
        }
    }
}
