// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using System.Runtime.ExceptionServices;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class QueryParser
    {
        public static class Monadic
        {
            public static TryCatch<SqlQuery> Parse(string text)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                AntlrInputStream str = new AntlrInputStream(text);
                sqlLexer lexer = new sqlLexer(str);
                CommonTokenStream tokens = new CommonTokenStream(lexer);
                sqlParser parser = new sqlParser(tokens)
                {
                    ErrorHandler = ThrowExceptionOnErrors.Singleton,
                };
                ErrorListener<IToken> listener = new ErrorListener<IToken>(parser, lexer, tokens);
                parser.AddErrorListener(listener);

                sqlParser.ProgramContext programContext;
                try
                {
                    programContext = parser.program();
                }
                catch (Exception ex)
                {
                    return TryCatch<SqlQuery>.FromException(ex);
                }

                if (listener.parseException != null)
                {
                    return TryCatch<SqlQuery>.FromException(listener.parseException);
                }

                SqlQuery sqlQuery = (SqlQuery)CstToAstVisitor.Singleton.Visit(programContext);
                return TryCatch<SqlQuery>.FromResult(sqlQuery);
            }

            private sealed class ThrowExceptionOnErrors : IAntlrErrorStrategy
            {
                public static readonly ThrowExceptionOnErrors Singleton = new ThrowExceptionOnErrors();

                public bool InErrorRecoveryMode(Parser recognizer)
                {
                    return false;
                }

                public void Recover(Parser recognizer, RecognitionException e)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }

                [return: NotNull]
                public IToken RecoverInline(Parser recognizer)
                {
                    throw new NotSupportedException("can not recover.");
                }

                public void ReportError(Parser recognizer, RecognitionException e)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }

                public void ReportMatch(Parser recognizer)
                {
                    // Do nothing
                }

                public void Reset(Parser recognizer)
                {
                    // Do nothing
                }

                public void Sync(Parser recognizer)
                {
                    // Do nothing
                }
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
            return false;
        }

        public static SqlQuery Parse(string text)
        {
            TryCatch<SqlQuery> monadicParse = Monadic.Parse(text);
            monadicParse.ThrowIfFailed();
            return monadicParse.Result;
        }
    }
}
