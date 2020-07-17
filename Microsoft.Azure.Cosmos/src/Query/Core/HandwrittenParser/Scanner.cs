// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.HandwrittenParser
{
    using System;

    internal ref struct Scanner
    {
        private ReadOnlySpan<char> corpus;

        public Scanner(ReadOnlySpan<char> corpus)
        {
            this.corpus = corpus;
            this.TokenValue = ReadOnlySpan<char>.Empty;
            this.Token = TokenKind.EOF;
        }

        public ReadOnlySpan<char> TokenValue { get; private set; }

        public TokenKind Token { get; private set; }

        public TokenKind Scan()
        {
            while (true)
            {
                if (this.corpus.IsEmpty)
                {
                    return this.Token = TokenKind.EOF;
                }

                char ch = this.corpus[0];
                switch (ch)
                {
                    case CharCodes.Space:
                    case CharCodes.Tab:
                    case CharCodes.LineFeed:
                    case CharCodes.CarriageReturn:
                        while (
                            (ch == CharCodes.Space) ||
                            (ch == CharCodes.Tab) ||
                            (ch == CharCodes.LineFeed) ||
                            (ch == CharCodes.CarriageReturn))
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            ch = this.corpus[0];
                        }

                        continue;

                    case CharCodes.OpenParen:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.OpenParen;

                    case CharCodes.CloseParen:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.CloseParen;

                    case CharCodes.OpenBracket:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.OpenBracket;

                    case CharCodes.CloseBracket:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.CloseBracket;

                    case CharCodes.OpenBrace:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.OpenBrace;

                    case CharCodes.CloseBrace:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.CloseBrace;

                    case CharCodes.Colon:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Colon;

                    case CharCodes.Asterisk:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Star;

                    case CharCodes.Plus:
                        if (char.IsDigit(this.corpus[1]) || this.corpus[1] == CharCodes.Dot)
                        {
                            return this.Token = this.ScanNumberLiteral();
                        }

                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Plus;

                    case CharCodes.Minus:
                        if (char.IsDigit(this.corpus[1]) || this.corpus[1] == CharCodes.Dot)
                        {
                            return this.Token = this.ScanNumberLiteral();
                        }

                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Minus;

                    case CharCodes.Ampersand:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Ampersand;

                    case CharCodes.Bar:
                        this.corpus = this.corpus.Slice(start: 1);
                        if (this.corpus[0] == CharCodes.Bar)
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.DoublePipe;
                        }

                        return this.Token = TokenKind.Pipe;

                    case CharCodes.Caret:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Caret;

                    case CharCodes.Slash:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Slash;

                    case CharCodes.EqualSign:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Equals;

                    case CharCodes.GreaterThan:
                        this.corpus = this.corpus.Slice(start: 1);
                        if (this.corpus[0] == CharCodes.EqualSign)
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.GreaterThanOrEquals;
                        }

                        return this.Token = TokenKind.GreaterThan;

                    case CharCodes.LessThan:
                        this.corpus = this.corpus.Slice(start: 1);
                        if (this.corpus[0] == CharCodes.EqualSign)
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.LessThanOrEquals;
                        }

                        return this.Token = TokenKind.LessThan;

                    case CharCodes.Percent:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Percent;

                    case CharCodes.Bang:
                        this.corpus = this.corpus.Slice(start: 1);
                        if (this.corpus[0] == CharCodes.EqualSign)
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.NotEquals;
                        }

                        return this.Token = TokenKind.Bang;

                    case CharCodes.Tilde:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Tilde;

                    case CharCodes.Comma:
                        this.corpus = this.corpus.Slice(start: 1);
                        return this.Token = TokenKind.Comma;

                    case CharCodes.SingleQuote:
                    case CharCodes.DoubleQuote:
                        return this.Token = this.ScanStringLiteral();

                    case CharCodes.Question:
                        this.corpus = this.corpus.Slice(start: 1);
                        if (this.corpus[0] == CharCodes.Question)
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.DoubleQuestion;
                        }

                        return this.Token = TokenKind.Question;

                    default:
                        if (Char.IsDigit(ch))
                        {
                            return this.Token = this.ScanNumberLiteral();
                        }
                        else if (IsIdStart(ch))
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            while (!this.corpus.IsEmpty && IsIdContinue(this.corpus[0]))
                            {
                                this.corpus = this.corpus.Slice(start: 1);
                            }

                            foreach (Keyword keyword in Keyword.Values)
                            {
                                if (this.corpus.StartsWith(
                                    keyword.Buffer.Span,
                                    keyword.CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase))
                                {
                                    return this.Token = keyword.TokenKind;
                                }
                            }

                            return this.Token = TokenKind.Identifier;
                        }
                        else
                        {
                            this.corpus = this.corpus.Slice(start: 1);
                            return this.Token = TokenKind.Unknown;
                        }
                }
            }
        }

        public void LookAhead(Action action)
        {
            ReadOnlySpan<char> oldCorpus = this.corpus;
            TokenKind oldToken = this.Token;
            ReadOnlySpan<char> oldTokenValue = this.TokenValue;

            action();

            this.corpus = oldCorpus;
            this.Token = oldToken;
            this.TokenValue = oldTokenValue;
        }

        private TokenKind ScanNumberLiteral()
        {
            ReadOnlySpan<char> numberCorpus = this.corpus;

            if ((this.corpus[0] == CharCodes.Plus) || (this.corpus[0] == CharCodes.Minus))
            {
                this.corpus = this.corpus.Slice(start: 1);
            }

            char startChar = this.corpus[0];

            while (!this.corpus.IsEmpty && char.IsDigit(this.corpus[0]))
            {
                this.corpus = this.corpus.Slice(start: 1);
            }

            if ((this.corpus[0] == CharCodes.Dot) && (startChar != CharCodes.Dot))
            {
                this.corpus = this.corpus.Slice(start: 1);
                while (!this.corpus.IsEmpty && char.IsDigit(this.corpus[0]))
                {
                    this.corpus = this.corpus.Slice(start: 1);
                }
            }

            if ((this.corpus[0] == CharCodes.e) || (this.corpus[0] == CharCodes.E))
            {
                this.corpus = this.corpus.Slice(start: 1);
                if ((this.corpus[0] == CharCodes.Plus) || (this.corpus[0] == CharCodes.Minus))
                {
                    this.corpus = this.corpus.Slice(start: 1);
                }

                while (!this.corpus.IsEmpty && char.IsDigit(this.corpus[0]))
                {
                    this.corpus = this.corpus.Slice(start: 1);
                }
            }

            numberCorpus = numberCorpus.Slice(start: 0, length: numberCorpus.Length - this.corpus.Length);
            this.TokenValue = numberCorpus;

            return TokenKind.NumericLiteral;
        }

        private TokenKind ScanStringLiteral()
        {
            ReadOnlySpan<char> stringCorpus = this.corpus;
            this.TokenValue = ReadOnlySpan<char>.Empty;
            char quote = this.corpus[0];
            this.corpus = this.corpus.Slice(start: 1);

            while (!this.corpus.IsEmpty && this.corpus[0] != quote)
            {
                if (this.corpus[0] == CharCodes.Backslash)
                {
                    if (this.corpus[1] == quote)
                    {
                        this.corpus = this.corpus.Slice(start: 2);
                    }
                }
            }

            // Consume the end string
            this.corpus = this.corpus.Slice(start: 1);

            this.TokenValue = stringCorpus.Slice(start: 0, length: stringCorpus.Length - this.corpus.Length);
            return TokenKind.StringLiteral;
        }

        private static bool IsIdStart(char ch) => (ch >= CharCodes.A && ch <= CharCodes.Z) || (ch >= CharCodes.a && ch <= CharCodes.z);

        private static bool IsIdContinue(char ch) => IsIdStart(ch) || Char.IsDigit(ch);
    }
}
