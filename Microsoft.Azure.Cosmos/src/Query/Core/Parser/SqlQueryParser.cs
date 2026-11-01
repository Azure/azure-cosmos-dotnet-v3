// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SqlObjects;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    static class SqlQueryParser
    {
        private const string SyntaxErrorMessage = "The query has a syntax error.";

        private const string NestingErrorMessage = "The query is nested too deeply to be parsed.";

        public static class Monadic
        {
            public static TryCatch<SqlQuery> Parse(string text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                SqlQuery sqlQuery;
                try
                {
                    // The grammar can accept inputs that are rejected when building the AST
                    // (e.g. an OFFSET / LIMIT / TOP literal that is not an integer or overflows Int64), these throw.
                    if (!SqlParlotParser.TryParse(text, out sqlQuery))
                    {
                        return TryCatch<SqlQuery>.FromException(new ParseException(SyntaxErrorMessage));
                    }
                }
                catch (InsufficientExecutionStackException ex)
                {
                    return TryCatch<SqlQuery>.FromException(new ParseException(NestingErrorMessage, ex));
                }
                catch (Exception ex)
                {
                    return TryCatch<SqlQuery>.FromException(ex);
                }

                return TryCatch<SqlQuery>.FromResult(sqlQuery);
            }
        }

        public static bool TryParse(string text, out SqlQuery sqlQuery)
        {
            TryCatch<SqlQuery> monadicParse = Monadic.Parse(text);
            if (monadicParse.Failed)
            {
                sqlQuery = default;
                return false;
            }

            sqlQuery = monadicParse.Result;
            return true;
        }

        public static SqlQuery Parse(string text)
        {
            TryCatch<SqlQuery> monadicParse = Monadic.Parse(text);
            monadicParse.ThrowIfFailed();
            return monadicParse.Result;
        }
    }
}
