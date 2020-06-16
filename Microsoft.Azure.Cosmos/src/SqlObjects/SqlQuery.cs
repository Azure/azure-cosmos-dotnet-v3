//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Runtime.ExceptionServices;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlQuery : SqlObject
    {
        private SqlQuery(
            SqlSelectClause selectClause,
            SqlFromClause fromClause,
            SqlWhereClause whereClause,
            SqlGroupByClause groupByClause,
            SqlOrderbyClause orderbyClause,
            SqlOffsetLimitClause offsetLimitClause)
        {
            this.SelectClause = selectClause ?? throw new ArgumentNullException(nameof(selectClause));
            this.FromClause = fromClause;
            this.WhereClause = whereClause;
            this.GroupByClause = groupByClause;
            this.OrderbyClause = orderbyClause;
            this.OffsetLimitClause = offsetLimitClause;
        }

        public SqlSelectClause SelectClause { get; }

        public SqlFromClause FromClause { get; }

        public SqlWhereClause WhereClause { get; }

        public SqlGroupByClause GroupByClause { get; }

        public SqlOrderbyClause OrderbyClause { get; }

        public SqlOffsetLimitClause OffsetLimitClause { get; }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public static SqlQuery Create(
            SqlSelectClause selectClause,
            SqlFromClause fromClause,
            SqlWhereClause whereClause,
            SqlGroupByClause groupByClause,
            SqlOrderbyClause orderByClause,
            SqlOffsetLimitClause offsetLimitClause) => new SqlQuery(
                selectClause,
                fromClause,
                whereClause,
                groupByClause,
                orderByClause,
                offsetLimitClause);

        public static bool TryParse(string text, out SqlQuery sqlQuery)
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
            catch (Exception)
            {
                sqlQuery = default;
                return false;
            }

            if (listener.hadError)
            {
                sqlQuery = default;
                return false;
            }

            sqlQuery = (SqlQuery)CstToAstVisitor.Singleton.Visit(programContext);
            return true;
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
}
