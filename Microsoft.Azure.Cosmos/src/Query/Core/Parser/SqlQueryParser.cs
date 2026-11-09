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
                    // A query nested too deeply for the remaining stack fails too, where the stack ran out.
                    if (!SqlParlotParser.TryParse(text, out sqlQuery, out (int Offset, int Line, int Column) failure))
                    {
                        return TryCatch<SqlQuery>.FromException(
                            new ParseException(
                                MayBeNestedTooDeeply(text, failure.Offset)
                                    ? $"The query has a syntax error at line {failure.Line}, column {failure.Column}, or is nested too deeply."
                                    : $"The query has a syntax error at line {failure.Line}, column {failure.Column}."));
                    }
                }
                catch (Exception ex)
                {
                    return TryCatch<SqlQuery>.FromException(ex);
                }

                return TryCatch<SqlQuery>.FromResult(sqlQuery);
            }

            // The parser reports reaching its depth limit, or the end of the stack, like a syntax error. Reaching the
            // limit takes at least 247 nested subqueries, so fewer tokens that open a level than that can't have.
            private static bool MayBeNestedTooDeeply(string text, int offset)
            {
                const int MinimumLevels = 150;

                int levels = 0;
                for (int i = 0; i < offset && i < text.Length; i++)
                {
                    switch (text[i])
                    {
                        case '(':
                        case '[':
                        case '{':
                        case '?':
                        case '-':
                        case '+':
                        case '~':
                        case 'n':
                        case 'N':
                            levels++;
                            break;
                    }
                }

                return levels >= MinimumLevels;
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
