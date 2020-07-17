// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.HandwrittenParser
{
    using System;
    using System.Collections.Generic;

    internal sealed class Keyword
    {
        public static readonly IReadOnlyList<Keyword> Values = new List<Keyword>()
        {
            new Keyword("and", TokenKind.AndKeyword, false),
            new Keyword("array", TokenKind.ArrayKeyword, false),
            new Keyword("as", TokenKind.AsKeyword, false),
            new Keyword("asc", TokenKind.AscKeyword, false),
            new Keyword("between", TokenKind.BetweenKeyword, false),
            new Keyword("by", TokenKind.ByKeyword, false),
            new Keyword("desc", TokenKind.DescKeyword, false),
            new Keyword("distinct", TokenKind.DistinctKeyword, false),
            new Keyword("exists", TokenKind.ExistsKeyword, false),
            new Keyword("false", TokenKind.FalseKeyword, true),
            new Keyword("from", TokenKind.FromKeyword, false),
            new Keyword("group", TokenKind.GroupKeyword, false),
            new Keyword("in", TokenKind.InKeyword, false),
            new Keyword("join", TokenKind.JoinKeyword, false),
            new Keyword("limit", TokenKind.LimitKeyword, false),
            new Keyword("not", TokenKind.NotKeyword, false),
            new Keyword("null", TokenKind.NullKeyword, true),
            new Keyword("offset", TokenKind.OffsetKeyword, false),
            new Keyword("or", TokenKind.OrKeyword, false),
            new Keyword("order", TokenKind.OrderKeyword, false),
            new Keyword("select", TokenKind.SelectKeyword, false),
            new Keyword("top", TokenKind.TopKeyword, false),
            new Keyword("true", TokenKind.TrueKeyword, true),
            new Keyword("udf", TokenKind.UdfKeyword, false),
            new Keyword("undefined", TokenKind.UndefinedKeyword, true),
            new Keyword("value", TokenKind.ValueKeyword, false),
            new Keyword("where", TokenKind.WhereKeyword, false),
        };

        private Keyword(string text, TokenKind tokenKind, bool caseSensitive)
        {
            this.Buffer = text.AsMemory();
            this.TokenKind = tokenKind;
            this.CaseSensitive = caseSensitive;
        }

        public ReadOnlyMemory<char> Buffer { get; }

        public TokenKind TokenKind { get; }

        public bool CaseSensitive { get; }
    }
}
