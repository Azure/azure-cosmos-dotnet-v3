//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlStringLiteral : SqlLiteral
    {
        public static SqlStringLiteral Empty = new SqlStringLiteral(string.Empty);
        private static readonly Dictionary<string, SqlStringLiteral> FrequentlyUsedStrings = new Dictionary<string, SqlStringLiteral>()
        {
            { string.Empty, SqlStringLiteral.Empty },
            { "root", new SqlStringLiteral("root") },

            // printable ascii characters
            { " ", new SqlStringLiteral(" ") },
            { "!", new SqlStringLiteral("!") },
            { "\"", new SqlStringLiteral("\"") },
            { "#", new SqlStringLiteral("#") },
            { "$", new SqlStringLiteral("$") },
            { "%", new SqlStringLiteral("%") },
            { "&", new SqlStringLiteral("&") },
            { "'", new SqlStringLiteral("'") },
            { "(", new SqlStringLiteral("(") },
            { ")", new SqlStringLiteral(")") },
            { "*", new SqlStringLiteral("*") },
            { "+", new SqlStringLiteral("+") },
            { ",", new SqlStringLiteral(",") },
            { "-", new SqlStringLiteral("-") },
            { ".", new SqlStringLiteral(".") },
            { "/", new SqlStringLiteral("/") },
            { "0", new SqlStringLiteral("0") },
            { "1", new SqlStringLiteral("1") },
            { "2", new SqlStringLiteral("2") },
            { "3", new SqlStringLiteral("3") },
            { "4", new SqlStringLiteral("4") },
            { "5", new SqlStringLiteral("5") },
            { "6", new SqlStringLiteral("6") },
            { "7", new SqlStringLiteral("7") },
            { "8", new SqlStringLiteral("8") },
            { "9", new SqlStringLiteral("9") },
            { ":", new SqlStringLiteral(":") },
            { ";", new SqlStringLiteral(";") },
            { "<", new SqlStringLiteral("<") },
            { "=", new SqlStringLiteral("=") },
            { ">", new SqlStringLiteral(">") },
            { "?", new SqlStringLiteral("?") },
            { "@", new SqlStringLiteral("@") },
            { "A", new SqlStringLiteral("A") },
            { "B", new SqlStringLiteral("B") },
            { "C", new SqlStringLiteral("C") },
            { "D", new SqlStringLiteral("D") },
            { "E", new SqlStringLiteral("E") },
            { "F", new SqlStringLiteral("F") },
            { "G", new SqlStringLiteral("G") },
            { "H", new SqlStringLiteral("H") },
            { "I", new SqlStringLiteral("I") },
            { "J", new SqlStringLiteral("J") },
            { "K", new SqlStringLiteral("K") },
            { "L", new SqlStringLiteral("L") },
            { "M", new SqlStringLiteral("M") },
            { "N", new SqlStringLiteral("N") },
            { "O", new SqlStringLiteral("O") },
            { "P", new SqlStringLiteral("P") },
            { "Q", new SqlStringLiteral("Q") },
            { "R", new SqlStringLiteral("R") },
            { "S", new SqlStringLiteral("S") },
            { "T", new SqlStringLiteral("T") },
            { "U", new SqlStringLiteral("U") },
            { "V", new SqlStringLiteral("V") },
            { "W", new SqlStringLiteral("W") },
            { "X", new SqlStringLiteral("X") },
            { "Y", new SqlStringLiteral("Y") },
            { "Z", new SqlStringLiteral("Z") },
            { "[", new SqlStringLiteral("[") },
            { "\\", new SqlStringLiteral("\\") },
            { "]", new SqlStringLiteral("]") },
            { "^", new SqlStringLiteral("^") },
            { "_", new SqlStringLiteral("_") },
            { "`", new SqlStringLiteral("`") },
            { "a", new SqlStringLiteral("a") },
            { "b", new SqlStringLiteral("b") },
            { "c", new SqlStringLiteral("c") },
            { "d", new SqlStringLiteral("d") },
            { "e", new SqlStringLiteral("e") },
            { "f", new SqlStringLiteral("f") },
            { "g", new SqlStringLiteral("g") },
            { "h", new SqlStringLiteral("h") },
            { "i", new SqlStringLiteral("i") },
            { "j", new SqlStringLiteral("j") },
            { "k", new SqlStringLiteral("k") },
            { "l", new SqlStringLiteral("l") },
            { "m", new SqlStringLiteral("m") },
            { "n", new SqlStringLiteral("n") },
            { "o", new SqlStringLiteral("o") },
            { "p", new SqlStringLiteral("p") },
            { "q", new SqlStringLiteral("q") },
            { "r", new SqlStringLiteral("r") },
            { "s", new SqlStringLiteral("s") },
            { "t", new SqlStringLiteral("t") },
            { "u", new SqlStringLiteral("u") },
            { "v", new SqlStringLiteral("v") },
            { "w", new SqlStringLiteral("w") },
            { "x", new SqlStringLiteral("x") },
            { "y", new SqlStringLiteral("y") },
            { "z", new SqlStringLiteral("z") },
            { "{", new SqlStringLiteral("{") },
            { "|", new SqlStringLiteral("|") },
            { "}", new SqlStringLiteral("}") },
            { "~", new SqlStringLiteral("~") },
            { "", new SqlStringLiteral("") },
            // p<x>
            { "p0", new SqlStringLiteral("p0") },
            { "p1", new SqlStringLiteral("p1") },
            { "p2", new SqlStringLiteral("p2") },
            { "p3", new SqlStringLiteral("p3") },
            { "p4", new SqlStringLiteral("p4") },
            { "p5", new SqlStringLiteral("p5") },
            { "p6", new SqlStringLiteral("p6") },
            { "p7", new SqlStringLiteral("p7") },
            { "p8", new SqlStringLiteral("p8") },
            { "p9", new SqlStringLiteral("p9") },
            { "p10", new SqlStringLiteral("p10") },
            { "p11", new SqlStringLiteral("p11") },
            { "p12", new SqlStringLiteral("p12") },
            { "p13", new SqlStringLiteral("p13") },
            { "p14", new SqlStringLiteral("p14") },
            { "p15", new SqlStringLiteral("p15") },
            { "p16", new SqlStringLiteral("p16") },
        };

        private SqlStringLiteral(string value)
            : base(SqlObjectKind.StringLiteral)
        {
            this.Value = value ?? throw new ArgumentNullException("value");
        }

        public string Value
        {
            get;
        }

        public static SqlStringLiteral Create(string value)
        {
            if (!SqlStringLiteral.FrequentlyUsedStrings.TryGetValue(value, out SqlStringLiteral sqlStringLiteral))
            {
                sqlStringLiteral = new SqlStringLiteral(value);
            }

            return sqlStringLiteral;
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlLiteralVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
