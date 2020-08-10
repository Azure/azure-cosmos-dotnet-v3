// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    using System;

    internal static class StringTokenLookup
    {
        public static StringToken Find(ReadOnlySpan<char> buffer) => buffer.Length switch
        {
            2 => FindLength2(buffer),
            3 => FindLength3(buffer),
            4 => FindLength4(buffer),
            5 => FindLength5(buffer),
            6 => FindLength6(buffer),
            7 => FindLength7(buffer),
            8 => FindLength8(buffer),
            9 => FindLength9(buffer),
            10 => FindLength10(buffer),
            11 => FindLength11(buffer),
            12 => FindLength12(buffer),
            13 => FindLength13(buffer),
            14 => FindLength14(buffer),
            15 => FindLength15(buffer),
            16 => FindLength16(buffer),
            17 => FindLength17(buffer),
            18 => FindLength18(buffer),
            19 => FindLength19(buffer),
            24 => FindLength24(buffer),
            _ => StringToken.NONE,
        };

        private static StringToken FindLength2(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            token = StringToken.As;
                            break;
                    }
                    break;
                case 'b':
                case 'B':
                    switch (buffer[index++])
                    {
                        case 'y':
                        case 'Y':
                            token = StringToken.By;
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            token = StringToken.In;
                            break;
                        case 's':
                        case 'S':
                            token = StringToken.Is;
                            break;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            token = StringToken.On;
                            break;
                        case 'r':
                        case 'R':
                            token = StringToken.Or;
                            break;
                    }
                    break;
                case 'p':
                case 'P':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            token = StringToken.Pi;
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength3(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'b':
                        case 'B':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    token = StringToken.Abs;
                                    break;
                            }
                            break;
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    token = StringToken.And;
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'c':
                                case 'C':
                                    token = StringToken.Asc;
                                    break;
                            }
                            break;
                        case 'v':
                        case 'V':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    token = StringToken.Avg;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    token = StringToken.Cos;
                                    break;
                                case 't':
                                case 'T':
                                    token = StringToken.Cot;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'e':
                case 'E':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    token = StringToken.End;
                                    break;
                            }
                            break;
                        case 'x':
                        case 'X':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    token = StringToken.Exp;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'f':
                case 'F':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    token = StringToken.For;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'l':
                case 'L':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    token = StringToken.Log;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'm':
                case 'M':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'x':
                                case 'X':
                                    token = StringToken.Max;
                                    break;
                            }
                            break;
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    token = StringToken.Min;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'n':
                case 'N':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            // Token 'NaN' is case sensitive
                            if (buffer.Equals("NaN".AsSpan(), StringComparison.InvariantCulture))
                            {
                                token = StringToken.NaN;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    token = StringToken.Not;
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    token = StringToken.Set;
                                    break;
                            }
                            break;
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    token = StringToken.Sin;
                                    break;
                            }
                            break;
                        case 'u':
                        case 'U':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    token = StringToken.Sum;
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    token = StringToken.Tan;
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    token = StringToken.Top;
                                    break;
                            }
                            break;
                    }
                    break;
                case 'u':
                case 'U':
                    // Token 'udf' is case sensitive
                    if (buffer.Equals("udf".AsSpan(), StringComparison.InvariantCulture))
                    {
                        token = StringToken.Udf;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength4(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'c':
                        case 'C':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            token = StringToken.Acos;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.Asin;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'a':
                                case 'A':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.Atan;
                                            break;
                                    }
                                    break;
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case '2':
                                            token = StringToken.Atn2;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            token = StringToken.Case;
                                            break;
                                        case 't':
                                        case 'T':
                                            token = StringToken.Cast;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'c':
                                        case 'C':
                                            token = StringToken.Desc;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'e':
                case 'E':
                    switch (buffer[index++])
                    {
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            token = StringToken.Else;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'f':
                case 'F':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'm':
                                        case 'M':
                                            token = StringToken.From;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'o':
                                        case 'O':
                                            token = StringToken.Into;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'j':
                case 'J':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.Join;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'l':
                case 'L':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'f':
                                case 'F':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            token = StringToken.Left;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'k':
                                case 'K':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            token = StringToken.Like;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'n':
                case 'N':
                    // Token 'null' is case sensitive
                    if (buffer.Equals("null".AsSpan(), StringComparison.InvariantCulture))
                    {
                        token = StringToken.Null;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'v':
                        case 'V':
                            switch (buffer[index++])
                            {
                                case 'e':
                                case 'E':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            token = StringToken.Over;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'r':
                case 'R':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'd':
                                        case 'D':
                                            token = StringToken.Rand;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.Sign;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'q':
                        case 'Q':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            token = StringToken.Sqrt;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'h':
                        case 'H':
                            switch (buffer[index++])
                            {
                                case 'e':
                                case 'E':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.Then;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'm':
                                        case 'M':
                                            token = StringToken.Trim;
                                            break;
                                    }
                                    break;
                                case 'u':
                                case 'U':
                                    // Token 'true' is case sensitive
                                    if (buffer.Equals("true".AsSpan(), StringComparison.InvariantCulture))
                                    {
                                        token = StringToken.True;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'w':
                case 'W':
                    switch (buffer[index++])
                    {
                        case 'h':
                        case 'H':
                            switch (buffer[index++])
                            {
                                case 'e':
                                case 'E':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            token = StringToken.When;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'h':
                                        case 'H':
                                            token = StringToken.With;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength5(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    token = StringToken.Array;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    token = StringToken.C_Map;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    token = StringToken.C_Set;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'd':
                                        case 'D':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    token = StringToken.C_Udt;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    token = StringToken.Count;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    token = StringToken.Cross;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'f':
                case 'F':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            // Token 'false' is case sensitive
                            if (buffer.Equals("false".AsSpan(), StringComparison.InvariantCulture))
                            {
                                token = StringToken.False;
                            }
                            break;
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'o':
                                        case 'O':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Floor;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'g':
                case 'G':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    token = StringToken.Group;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Inner;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'l':
                case 'L':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    token = StringToken.Limit;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    switch (buffer[index++])
                                    {
                                        case '1':
                                            switch (buffer[index++])
                                            {
                                                case '0':
                                                    token = StringToken.Log10;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'w':
                                case 'W':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Lower;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'm':
                                                case 'M':
                                                    token = StringToken.Ltrim;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Order;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'u':
                        case 'U':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Outer;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'p':
                case 'P':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'w':
                                case 'W':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Power;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'r':
                case 'R':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    switch (buffer[index++])
                                    {
                                        case 'h':
                                        case 'H':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    token = StringToken.Right;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 'd':
                                                case 'D':
                                                    token = StringToken.Round;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'm':
                                                case 'M':
                                                    token = StringToken.Rtrim;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 'c':
                                                case 'C':
                                                    token = StringToken.Trunc;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'u':
                case 'U':
                    switch (buffer[index++])
                    {
                        case 'p':
                        case 'P':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    token = StringToken.Upper;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'v':
                case 'V':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'l':
                                case 'L':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    token = StringToken.Value;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'w':
                case 'W':
                    switch (buffer[index++])
                    {
                        case 'h':
                        case 'H':
                            switch (buffer[index++])
                            {
                                case 'e':
                                case 'E':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    token = StringToken.Where;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength6(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'd':
                                                        case 'D':
                                                            token = StringToken.C_Guid;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case '8':
                                                            token = StringToken.C_Int8;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'l':
                                case 'L':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            token = StringToken.C_List;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'c':
                                        case 'C':
                                            switch (buffer[index++])
                                            {
                                                case 'a':
                                                case 'A':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            token = StringToken.Concat;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'e':
                case 'E':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'c':
                                case 'C':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            token = StringToken.Escape;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'x':
                        case 'X':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 's':
                                                        case 'S':
                                                            token = StringToken.Exists;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'h':
                case 'H':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'v':
                                case 'V':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            token = StringToken.Having;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            token = StringToken.Insert;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'b':
                                case 'B':
                                    switch (buffer[index++])
                                    {
                                        case 'o':
                                        case 'O':
                                            switch (buffer[index++])
                                            {
                                                case 'o':
                                                case 'O':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'l':
                                                        case 'L':
                                                            token = StringToken.IsBool;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'l':
                                                case 'L':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'l':
                                                        case 'L':
                                                            token = StringToken.IsNull;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'l':
                case 'L':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'g':
                                        case 'G':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'h':
                                                        case 'H':
                                                            token = StringToken.Length;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'f':
                        case 'F':
                            switch (buffer[index++])
                            {
                                case 'f':
                                case 'F':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            token = StringToken.Offset;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'l':
                                case 'L':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'c':
                                                case 'C':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            token = StringToken.Select;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'q':
                        case 'Q':
                            switch (buffer[index++])
                            {
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            token = StringToken.Square;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'u':
                case 'U':
                    switch (buffer[index++])
                    {
                        case 'p':
                        case 'P':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            token = StringToken.Update;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength7(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'b':
                case 'B':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'w':
                                        case 'W':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    token = StringToken.Between;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case '1':
                                                            switch (buffer[index++])
                                                            {
                                                                case '6':
                                                                    token = StringToken.C_Int16;
                                                                    break;
                                                            }
                                                            break;
                                                        case '3':
                                                            switch (buffer[index++])
                                                            {
                                                                case '2':
                                                                    token = StringToken.C_Int32;
                                                                    break;
                                                            }
                                                            break;
                                                        case '6':
                                                            switch (buffer[index++])
                                                            {
                                                                case '4':
                                                                    token = StringToken.C_Int64;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'l':
                                                        case 'L':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    token = StringToken.C_Tuple;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 'l':
                                        case 'L':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'n':
                                                        case 'N':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'g':
                                                                case 'G':
                                                                    token = StringToken.Ceiling;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'v':
                                        case 'V':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    token = StringToken.Convert;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'g':
                                case 'G':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            switch (buffer[index++])
                                                            {
                                                                case 's':
                                                                case 'S':
                                                                    token = StringToken.Degrees;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'x':
                                                case 'X':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'o':
                                                        case 'O':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'f':
                                                                case 'F':
                                                                    token = StringToken.IndexOf;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'b':
                                        case 'B':
                                            switch (buffer[index++])
                                            {
                                                case 'o':
                                                case 'O':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'o':
                                                        case 'O':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    token = StringToken.Is_Bool;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 'u':
                                                case 'U':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'l':
                                                        case 'L':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    token = StringToken.Is_Null;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'a':
                                case 'A':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'y':
                                                                case 'Y':
                                                                    token = StringToken.IsArray;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'r':
                case 'R':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'a':
                                                case 'A':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'n':
                                                        case 'N':
                                                            switch (buffer[index++])
                                                            {
                                                                case 's':
                                                                case 'S':
                                                                    token = StringToken.Radians;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    switch (buffer[index++])
                                    {
                                        case 'l':
                                        case 'L':
                                            switch (buffer[index++])
                                            {
                                                case 'a':
                                                case 'A':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    token = StringToken.Replace;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'v':
                                case 'V':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 's':
                                                        case 'S':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    token = StringToken.Reverse;
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength8(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'b':
                                case 'B':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'y':
                                                                        case 'Y':
                                                                            token = StringToken.C_Binary;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'u':
                                case 'U':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case '3':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case '2':
                                                                            token = StringToken.C_UInt32;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'a':
                                                case 'A':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 's':
                                                                        case 'S':
                                                                            token = StringToken.Contains;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'n':
                                                        case 'N':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'c':
                                                                case 'C':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            token = StringToken.Distinct;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'e':
                case 'E':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 'w':
                                                case 'W':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'h':
                                                                        case 'H':
                                                                            token = StringToken.EndsWith;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 'n':
                        case 'N':
                            switch (buffer[index++])
                            {
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'x':
                                                case 'X':
                                                    switch (buffer[index++])
                                                    {
                                                        case '_':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'f':
                                                                        case 'F':
                                                                            token = StringToken.Index_Of;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'f':
                                case 'F':
                                    // Token 'Infinity' is case sensitive
                                    if (buffer.Equals("Infinity".AsSpan(), StringComparison.InvariantCulture))
                                    {
                                        token = StringToken.Infinity;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'y':
                                                                        case 'Y':
                                                                            token = StringToken.Is_Array;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'n':
                                case 'N':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'm':
                                                case 'M':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'b':
                                                        case 'B':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'r':
                                                                        case 'R':
                                                                            token = StringToken.IsNumber;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'b':
                                        case 'B':
                                            switch (buffer[index++])
                                            {
                                                case 'j':
                                                case 'J':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'c':
                                                                case 'C':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            token = StringToken.IsObject;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'g':
                                                                        case 'G':
                                                                            token = StringToken.IsString;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'g':
                                                                        case 'G':
                                                                            token = StringToken.ToString;
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength9(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'f':
                                case 'F':
                                    switch (buffer[index++])
                                    {
                                        case 'l':
                                        case 'L':
                                            switch (buffer[index++])
                                            {
                                                case 'o':
                                                case 'O':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case '3':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case '2':
                                                                                    token = StringToken.C_Float32;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                        case '6':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case '4':
                                                                                    token = StringToken.C_Float64;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'n':
                                        case 'N':
                                            switch (buffer[index++])
                                            {
                                                case 'u':
                                                case 'U':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'm':
                                                        case 'M':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'b':
                                                                case 'B':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'r':
                                                                                case 'R':
                                                                                    token = StringToken.Is_Number;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                        case 'o':
                                        case 'O':
                                            switch (buffer[index++])
                                            {
                                                case 'b':
                                                case 'B':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'j':
                                                        case 'J':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'c':
                                                                        case 'C':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    token = StringToken.Is_Object;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'g':
                                                                                case 'G':
                                                                                    token = StringToken.Is_String;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'b':
                                case 'B':
                                    switch (buffer[index++])
                                    {
                                        case 'o':
                                        case 'O':
                                            switch (buffer[index++])
                                            {
                                                case 'o':
                                                case 'O':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'l':
                                                        case 'L':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'a':
                                                                        case 'A':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    token = StringToken.IsBoolean;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'd':
                                case 'D':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'f':
                                                case 'F':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'd':
                                                                                case 'D':
                                                                                    token = StringToken.IsDefined;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'r':
                case 'R':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    switch (buffer[index++])
                                    {
                                        case 'l':
                                        case 'L':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'e':
                                                                                case 'E':
                                                                                    token = StringToken.Replicate;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'w':
                                        case 'W':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'h':
                                                                case 'H':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'i':
                                                                        case 'I':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    token = StringToken.ST_Within;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'u':
                        case 'U':
                            switch (buffer[index++])
                            {
                                case 'b':
                                case 'B':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'g':
                                                                                case 'G':
                                                                                    token = StringToken.Substring;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 's':
                                        case 'S':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'g':
                                                                                case 'G':
                                                                                    token = StringToken.To_String;
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'u':
                case 'U':
                    // Token 'undefined' is case sensitive
                    if (buffer.Equals("undefined".AsSpan(), StringComparison.InvariantCulture))
                    {
                        token = StringToken.Undefined;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength10(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'm':
                        case 'M':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'v':
                                                case 'V':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case '_':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'e':
                                                                                case 'E':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'q':
                                                                                        case 'Q':
                                                                                            token = StringToken._M_Eval_Eq;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'g':
                                                                                case 'G':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            token = StringToken._M_Eval_Gt;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'n':
                                                                                        case 'N':
                                                                                            token = StringToken._M_Eval_In;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'l':
                                                                                case 'L':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            token = StringToken._M_Eval_Lt;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case '_':
                                            switch (buffer[index++])
                                            {
                                                case 'w':
                                                case 'W':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'h':
                                                                        case 'H':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'n':
                                                                                        case 'N':
                                                                                            token = StringToken._ST_Within;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    switch (buffer[index++])
                                                    {
                                                        case 's':
                                                        case 'S':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'i':
                                                                        case 'I':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'c':
                                                                                case 'C':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'e':
                                                                                        case 'E':
                                                                                            token = StringToken.ArraySlice;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'c':
                                case 'C':
                                    switch (buffer[index++])
                                    {
                                        case 'u':
                                        case 'U':
                                            switch (buffer[index++])
                                            {
                                                case 'm':
                                                case 'M':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'e':
                                                        case 'E':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'n':
                                                                case 'N':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'd':
                                                                                        case 'D':
                                                                                            token = StringToken.DocumentId;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'b':
                                        case 'B':
                                            switch (buffer[index++])
                                            {
                                                case 'o':
                                                case 'O':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'o':
                                                        case 'O':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'n':
                                                                                        case 'N':
                                                                                            token = StringToken.Is_Boolean;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                        case 'd':
                                        case 'D':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'f':
                                                        case 'F':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'e':
                                                                                case 'E':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'd':
                                                                                        case 'D':
                                                                                            token = StringToken.Is_Defined;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'v':
                                                        case 'V':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'd':
                                                                                        case 'D':
                                                                                            token = StringToken.ST_IsValid;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'a':
                                case 'A':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 's':
                                                        case 'S':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'w':
                                                                case 'W':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'i':
                                                                        case 'I':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'h':
                                                                                        case 'H':
                                                                                            token = StringToken.StartsWith;
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength11(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'm':
                        case 'M':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'v':
                                                case 'V':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'l':
                                                                case 'L':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case '_':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'g':
                                                                                case 'G':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    token = StringToken._M_Eval_Gte;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'l':
                                                                                case 'L':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    token = StringToken._M_Eval_Lte;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'e':
                                                                                        case 'E':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'q':
                                                                                                case 'Q':
                                                                                                    token = StringToken._M_Eval_Neq;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                        case 'i':
                                                                                        case 'I':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'n':
                                                                                                case 'N':
                                                                                                    token = StringToken._M_Eval_Nin;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    switch (buffer[index++])
                                                    {
                                                        case '_':
                                                            switch (buffer[index++])
                                                            {
                                                                case 's':
                                                                case 'S':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'c':
                                                                                        case 'C':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    token = StringToken.Array_Slice;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'c':
                                                                                case 'C':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    token = StringToken.ArrayConcat;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                        case 'l':
                                                        case 'L':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'g':
                                                                                case 'G':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'h':
                                                                                                case 'H':
                                                                                                    token = StringToken.ArrayLength;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'd':
                                                                                        case 'D':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'd':
                                                                                                case 'D':
                                                                                                    token = StringToken.DateTimeAdd;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'p':
                                case 'P':
                                    switch (buffer[index++])
                                    {
                                        case 'r':
                                        case 'R':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'm':
                                                        case 'M':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'v':
                                                                                        case 'V':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    token = StringToken.IsPrimitive;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'd':
                                        case 'D':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 's':
                                                        case 'S':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'a':
                                                                        case 'A':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'c':
                                                                                        case 'C':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    token = StringToken.ST_Distance;
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength12(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'e':
                                case 'E':
                                    switch (buffer[index++])
                                    {
                                        case 'g':
                                        case 'G':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'x':
                                                        case 'X':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'm':
                                                                        case 'M':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'h':
                                                                                                        case 'H':
                                                                                                            token = StringToken._Regex_Match;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case '_':
                                            switch (buffer[index++])
                                            {
                                                case 'd':
                                                case 'D':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 's':
                                                                case 'S':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'n':
                                                                                        case 'N':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'e':
                                                                                                        case 'E':
                                                                                                            token = StringToken._ST_Distance;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    switch (buffer[index++])
                                                    {
                                                        case '_':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'c':
                                                                case 'C':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'c':
                                                                                        case 'C':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 't':
                                                                                                        case 'T':
                                                                                                            token = StringToken.Array_Concat;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                                case 'l':
                                                                case 'L':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'g':
                                                                                        case 'G':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'h':
                                                                                                        case 'H':
                                                                                                            token = StringToken.Array_Length;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'd':
                                                                                case 'D':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'i':
                                                                                        case 'I':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'f':
                                                                                                case 'F':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'f':
                                                                                                        case 'F':
                                                                                                            token = StringToken.DateTimeDiff;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'p':
                                                                                case 'P':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'r':
                                                                                                case 'R':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 't':
                                                                                                        case 'T':
                                                                                                            token = StringToken.DateTimePart;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'p':
                                        case 'P':
                                            switch (buffer[index++])
                                            {
                                                case 'r':
                                                case 'R':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'i':
                                                                        case 'I':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'i':
                                                                                        case 'I':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'v':
                                                                                                case 'V':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'e':
                                                                                                        case 'E':
                                                                                                            token = StringToken.Is_Primitive;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'q':
                                                                        case 'Q':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'u':
                                                                                case 'U':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'l':
                                                                                                case 'L':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 's':
                                                                                                        case 'S':
                                                                                                            token = StringToken.StringEquals;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'u':
                                                                                        case 'U':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'l':
                                                                                                case 'L':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'l':
                                                                                                        case 'L':
                                                                                                            token = StringToken.StringToNull;
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength13(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'i':
                                                                                                case 'I':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'n':
                                                                                                        case 'N':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    token = StringToken.ArrayContains;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'i':
                                                                                                case 'I':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'n':
                                                                                                        case 'N':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    token = StringToken.C_MapContains;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 's':
                                case 'S':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'i':
                                                                                                case 'I':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'n':
                                                                                                        case 'N':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    token = StringToken.C_SetContains;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'b':
                        case 'B':
                            switch (buffer[index++])
                            {
                                case 'j':
                                case 'J':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'c':
                                                case 'C':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'r':
                                                                                        case 'R':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'r':
                                                                                                case 'R':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'a':
                                                                                                        case 'A':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'y':
                                                                                                                case 'Y':
                                                                                                                    token = StringToken.ObjectToArray;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'e':
                                                                case 'E':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'r':
                                                                        case 'R':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 's':
                                                                                case 'S':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'e':
                                                                                        case 'E':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 't':
                                                                                                        case 'T':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    token = StringToken.ST_Intersects;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'q':
                                                                                case 'Q':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'u':
                                                                                        case 'U':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'l':
                                                                                                        case 'L':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    token = StringToken.String_Equals;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'r':
                                                                                        case 'R':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'r':
                                                                                                case 'R':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'a':
                                                                                                        case 'A':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'y':
                                                                                                                case 'Y':
                                                                                                                    token = StringToken.StringToArray;
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength14(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'o':
                        case 'O':
                            switch (buffer[index++])
                            {
                                case 'b':
                                case 'B':
                                    switch (buffer[index++])
                                    {
                                        case 'j':
                                        case 'J':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'r':
                                                                                                case 'R':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'r':
                                                                                                        case 'R':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'a':
                                                                                                                case 'A':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'y':
                                                                                                                        case 'Y':
                                                                                                                            token = StringToken._ObjectToArray;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case '_':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'n':
                                                        case 'N':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'r':
                                                                                case 'R':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 's':
                                                                                        case 'S':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'c':
                                                                                                        case 'C':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 't':
                                                                                                                case 'T':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 's':
                                                                                                                        case 'S':
                                                                                                                            token = StringToken._ST_Intersects;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'a':
                case 'A':
                    switch (buffer[index++])
                    {
                        case 'r':
                        case 'R':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'y':
                                                case 'Y':
                                                    switch (buffer[index++])
                                                    {
                                                        case '_':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'c':
                                                                case 'C':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'n':
                                                                                                                case 'N':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 's':
                                                                                                                        case 'S':
                                                                                                                            token = StringToken.Array_Contains;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'l':
                                case 'L':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'c':
                                                                case 'C':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'n':
                                                                                                                case 'N':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 's':
                                                                                                                        case 'S':
                                                                                                                            token = StringToken.C_ListContains;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case 'f':
                                case 'F':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'u':
                                                                                        case 'U':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'm':
                                                                                                case 'M':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'b':
                                                                                                        case 'B':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'e':
                                                                                                                case 'E':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'r':
                                                                                                                        case 'R':
                                                                                                                            token = StringToken.IsFiniteNumber;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'n':
                                                                                                case 'N':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'u':
                                                                                                        case 'U':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'l':
                                                                                                                case 'L':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'l':
                                                                                                                        case 'L':
                                                                                                                            token = StringToken.String_To_Null;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'u':
                                                                                        case 'U':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'm':
                                                                                                case 'M':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'b':
                                                                                                        case 'B':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'e':
                                                                                                                case 'E':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'r':
                                                                                                                        case 'R':
                                                                                                                            token = StringToken.StringToNumber;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'b':
                                                                                        case 'B':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'j':
                                                                                                case 'J':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'e':
                                                                                                        case 'E':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'c':
                                                                                                                case 'C':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 't':
                                                                                                                        case 'T':
                                                                                                                            token = StringToken.StringToObject;
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength15(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 's':
                                                                                        case 'S':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'g':
                                                                                                        case 'G':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'u':
                                                                                                                case 'U':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'i':
                                                                                                                        case 'I':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'd':
                                                                                                                                case 'D':
                                                                                                                                    token = StringToken._LiteralAsCGuid;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'n':
                                                                                                                case 'N':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 't':
                                                                                                                        case 'T':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case '8':
                                                                                                                                    token = StringToken._LiteralAsCInt8;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'o':
                                                                                        case 'O':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'c':
                                                                                                                case 'C':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'k':
                                                                                                                        case 'K':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 's':
                                                                                                                                case 'S':
                                                                                                                                    token = StringToken.DateTimeToTicks;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'g':
                case 'G':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'c':
                                        case 'C':
                                            switch (buffer[index++])
                                            {
                                                case 'u':
                                                case 'U':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'c':
                                                                                                                case 'C':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'k':
                                                                                                                        case 'K':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 's':
                                                                                                                                case 'S':
                                                                                                                                    token = StringToken.GetCurrentTicks;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'o':
                case 'O':
                    switch (buffer[index++])
                    {
                        case 'b':
                        case 'B':
                            switch (buffer[index++])
                            {
                                case 'j':
                                case 'J':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 'c':
                                                case 'C':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'r':
                                                                                                        case 'R':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'r':
                                                                                                                case 'R':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'a':
                                                                                                                        case 'A':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'y':
                                                                                                                                case 'Y':
                                                                                                                                    token = StringToken.Object_To_Array;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'a':
                                                                                                case 'A':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'r':
                                                                                                        case 'R':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'r':
                                                                                                                case 'R':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'a':
                                                                                                                        case 'A':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'y':
                                                                                                                                case 'Y':
                                                                                                                                    token = StringToken.String_To_Array;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                                case 't':
                                                                case 'T':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'o':
                                                                        case 'O':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'b':
                                                                                case 'B':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'o':
                                                                                        case 'O':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'o':
                                                                                                case 'O':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'l':
                                                                                                        case 'L':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'e':
                                                                                                                case 'E':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'a':
                                                                                                                        case 'A':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'n':
                                                                                                                                case 'N':
                                                                                                                                    token = StringToken.StringToBoolean;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'c':
                                case 'C':
                                    switch (buffer[index++])
                                    {
                                        case 'k':
                                        case 'K':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'd':
                                                                        case 'D':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'e':
                                                                                                case 'E':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 't':
                                                                                                        case 'T':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'i':
                                                                                                                case 'I':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'm':
                                                                                                                        case 'M':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    token = StringToken.TicksToDateTime;
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength16(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'c':
                        case 'C':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'm':
                                        case 'M':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case '_':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'o':
                                                                                        case 'O':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'b':
                                                                                                case 'B':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'j':
                                                                                                        case 'J':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'e':
                                                                                                                case 'E':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'c':
                                                                                                                        case 'C':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 't':
                                                                                                                                case 'T':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 's':
                                                                                                                                        case 'S':
                                                                                                                                            token = StringToken._Compare_Objects;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 's':
                                                                                        case 'S':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'n':
                                                                                                                case 'N':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 't':
                                                                                                                        case 'T':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case '1':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case '6':
                                                                                                                                            token = StringToken._LiteralAsCInt16;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                                case '3':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case '2':
                                                                                                                                            token = StringToken._LiteralAsCInt32;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                                case '6':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case '4':
                                                                                                                                            token = StringToken._LiteralAsCInt64;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'i':
                                                                                                case 'I':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'n':
                                                                                                        case 'N':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'k':
                                                                                                                        case 'K':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'y':
                                                                                                                                        case 'Y':
                                                                                                                                            token = StringToken.C_MapContainsKey;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'i':
                case 'I':
                    switch (buffer[index++])
                    {
                        case 's':
                        case 'S':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'f':
                                        case 'F':
                                            switch (buffer[index++])
                                            {
                                                case 'i':
                                                case 'I':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'n':
                                                        case 'N':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'i':
                                                                case 'I':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'e':
                                                                                case 'E':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'n':
                                                                                                case 'N':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'u':
                                                                                                        case 'U':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'm':
                                                                                                                case 'M':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'b':
                                                                                                                        case 'B':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'r':
                                                                                                                                        case 'R':
                                                                                                                                            token = StringToken.Is_Finite_Number;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'n':
                                                                                                case 'N':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'u':
                                                                                                        case 'U':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'm':
                                                                                                                case 'M':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'b':
                                                                                                                        case 'B':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'r':
                                                                                                                                        case 'R':
                                                                                                                                            token = StringToken.String_To_Number;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                                case 'o':
                                                                                                case 'O':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'b':
                                                                                                        case 'B':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'j':
                                                                                                                case 'J':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'e':
                                                                                                                        case 'E':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'c':
                                                                                                                                case 'C':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            token = StringToken.String_To_Object;
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength17(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 's':
                                                                                        case 'S':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'b':
                                                                                                        case 'B':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'i':
                                                                                                                case 'I':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'n':
                                                                                                                        case 'N':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'a':
                                                                                                                                case 'A':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'r':
                                                                                                                                        case 'R':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'y':
                                                                                                                                                case 'Y':
                                                                                                                                                    token = StringToken._LiteralAsCBinary;
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                        case 'u':
                                                                                                        case 'U':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'i':
                                                                                                                case 'I':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'n':
                                                                                                                        case 'N':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 't':
                                                                                                                                case 'T':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case '3':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case '2':
                                                                                                                                                    token = StringToken._LiteralAsCUInt32;
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'p':
                        case 'P':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'o':
                                        case 'O':
                                            switch (buffer[index++])
                                            {
                                                case 'x':
                                                case 'X':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'y':
                                                        case 'Y':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'p':
                                                                        case 'P':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'r':
                                                                                case 'R':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'o':
                                                                                        case 'O':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'j':
                                                                                                case 'J':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'e':
                                                                                                        case 'E':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'c':
                                                                                                                case 'C':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 't':
                                                                                                                        case 'T':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'i':
                                                                                                                                case 'I':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'o':
                                                                                                                                        case 'O':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'n':
                                                                                                                                                case 'N':
                                                                                                                                                    token = StringToken._Proxy_Projection;
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'f':
                                                                                case 'F':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'r':
                                                                                        case 'R':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'o':
                                                                                                case 'O':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'm':
                                                                                                        case 'M':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'p':
                                                                                                                case 'P':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'a':
                                                                                                                        case 'A':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'r':
                                                                                                                                case 'R':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 's':
                                                                                                                                                case 'S':
                                                                                                                                                    token = StringToken.DateTimeFromParts;
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 'n':
                                                case 'N':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'g':
                                                        case 'G':
                                                            switch (buffer[index++])
                                                            {
                                                                case '_':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 't':
                                                                        case 'T':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'o':
                                                                                case 'O':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case '_':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'b':
                                                                                                case 'B':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'o':
                                                                                                        case 'O':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'o':
                                                                                                                case 'O':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'l':
                                                                                                                        case 'L':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'a':
                                                                                                                                        case 'A':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'n':
                                                                                                                                                case 'N':
                                                                                                                                                    token = StringToken.String_To_Boolean;
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength18(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'l':
                        case 'L':
                            switch (buffer[index++])
                            {
                                case 'i':
                                case 'I':
                                    switch (buffer[index++])
                                    {
                                        case 't':
                                        case 'T':
                                            switch (buffer[index++])
                                            {
                                                case 'e':
                                                case 'E':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 's':
                                                                                        case 'S':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'c':
                                                                                                case 'C':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'f':
                                                                                                        case 'F':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'l':
                                                                                                                case 'L':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'o':
                                                                                                                        case 'O':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'a':
                                                                                                                                case 'A':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case '3':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case '2':
                                                                                                                                                            token = StringToken._LiteralAsCFloat32;
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                                case '6':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case '4':
                                                                                                                                                            token = StringToken._LiteralAsCFloat64;
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'c':
                case 'C':
                    switch (buffer[index++])
                    {
                        case '_':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'a':
                                        case 'A':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'c':
                                                        case 'C':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'o':
                                                                case 'O':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'n':
                                                                        case 'N':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'a':
                                                                                        case 'A':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'i':
                                                                                                case 'I':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'n':
                                                                                                        case 'N':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 's':
                                                                                                                case 'S':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'v':
                                                                                                                        case 'V':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'a':
                                                                                                                                case 'A':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'l':
                                                                                                                                        case 'L':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'u':
                                                                                                                                                case 'U':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'e':
                                                                                                                                                        case 'E':
                                                                                                                                                            token = StringToken.C_MapContainsValue;
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'g':
                case 'G':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'c':
                                        case 'C':
                                            switch (buffer[index++])
                                            {
                                                case 'u':
                                                case 'U':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'd':
                                                                                                case 'D':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'a':
                                                                                                        case 'A':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 't':
                                                                                                                case 'T':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'e':
                                                                                                                        case 'E':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 't':
                                                                                                                                case 'T':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'i':
                                                                                                                                        case 'I':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'm':
                                                                                                                                                case 'M':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'e':
                                                                                                                                                        case 'E':
                                                                                                                                                            token = StringToken.GetCurrentDateTime;
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 's':
                case 'S':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case '_':
                                    switch (buffer[index++])
                                    {
                                        case 'i':
                                        case 'I':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'v':
                                                        case 'V':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'l':
                                                                        case 'L':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'i':
                                                                                case 'I':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'd':
                                                                                        case 'D':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'd':
                                                                                                case 'D':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'e':
                                                                                                        case 'E':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 't':
                                                                                                                case 'T':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'a':
                                                                                                                        case 'A':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'i':
                                                                                                                                case 'I':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'l':
                                                                                                                                        case 'L':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'e':
                                                                                                                                                case 'E':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'd':
                                                                                                                                                        case 'D':
                                                                                                                                                            token = StringToken.ST_IsValidDetailed;
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength19(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 't':
                        case 'T':
                            switch (buffer[index++])
                            {
                                case 'r':
                                case 'R':
                                    switch (buffer[index++])
                                    {
                                        case 'y':
                                        case 'Y':
                                            switch (buffer[index++])
                                            {
                                                case '_':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'r':
                                                                        case 'R':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'a':
                                                                                case 'A':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'y':
                                                                                        case 'Y':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case '_':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'c':
                                                                                                        case 'C':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'o':
                                                                                                                case 'O':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'n':
                                                                                                                        case 'N':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 't':
                                                                                                                                case 'T':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'a':
                                                                                                                                        case 'A':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'i':
                                                                                                                                                case 'I':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'n':
                                                                                                                                                        case 'N':
                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                            {
                                                                                                                                                                case 's':
                                                                                                                                                                case 'S':
                                                                                                                                                                    token = StringToken._Try_Array_Contains;
                                                                                                                                                                    break;
                                                                                                                                                            }
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'd':
                case 'D':
                    switch (buffer[index++])
                    {
                        case 'a':
                        case 'A':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 't':
                                                case 'T':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'i':
                                                        case 'I':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'm':
                                                                case 'M':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 't':
                                                                                case 'T':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'o':
                                                                                        case 'O':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'm':
                                                                                                                case 'M':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'e':
                                                                                                                        case 'E':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 's':
                                                                                                                                case 'S':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'a':
                                                                                                                                                case 'A':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'm':
                                                                                                                                                        case 'M':
                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                            {
                                                                                                                                                                case 'p':
                                                                                                                                                                case 'P':
                                                                                                                                                                    token = StringToken.DateTimeToTimestamp;
                                                                                                                                                                    break;
                                                                                                                                                            }
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 'g':
                case 'G':
                    switch (buffer[index++])
                    {
                        case 'e':
                        case 'E':
                            switch (buffer[index++])
                            {
                                case 't':
                                case 'T':
                                    switch (buffer[index++])
                                    {
                                        case 'c':
                                        case 'C':
                                            switch (buffer[index++])
                                            {
                                                case 'u':
                                                case 'U':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'r':
                                                        case 'R':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'n':
                                                                                case 'N':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 't':
                                                                                                case 'T':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'i':
                                                                                                        case 'I':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'm':
                                                                                                                case 'M':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 'e':
                                                                                                                        case 'E':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 's':
                                                                                                                                case 'S':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'a':
                                                                                                                                                case 'A':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'm':
                                                                                                                                                        case 'M':
                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                            {
                                                                                                                                                                case 'p':
                                                                                                                                                                case 'P':
                                                                                                                                                                    token = StringToken.GetCurrentTimestamp;
                                                                                                                                                                    break;
                                                                                                                                                            }
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case 't':
                case 'T':
                    switch (buffer[index++])
                    {
                        case 'i':
                        case 'I':
                            switch (buffer[index++])
                            {
                                case 'm':
                                case 'M':
                                    switch (buffer[index++])
                                    {
                                        case 'e':
                                        case 'E':
                                            switch (buffer[index++])
                                            {
                                                case 's':
                                                case 'S':
                                                    switch (buffer[index++])
                                                    {
                                                        case 't':
                                                        case 'T':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'a':
                                                                case 'A':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'm':
                                                                        case 'M':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case 'p':
                                                                                case 'P':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 't':
                                                                                        case 'T':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 'o':
                                                                                                case 'O':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'd':
                                                                                                        case 'D':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'a':
                                                                                                                case 'A':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case 't':
                                                                                                                        case 'T':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'e':
                                                                                                                                case 'E':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 't':
                                                                                                                                        case 'T':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'i':
                                                                                                                                                case 'I':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'm':
                                                                                                                                                        case 'M':
                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                            {
                                                                                                                                                                case 'e':
                                                                                                                                                                case 'E':
                                                                                                                                                                    token = StringToken.TimestampToDateTime;
                                                                                                                                                                    break;
                                                                                                                                                            }
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
        private static StringToken FindLength24(ReadOnlySpan<char> buffer)
        {
            StringToken token = StringToken.NONE;

            int index = 0;

            switch (buffer[index++])
            {
                case '_':
                    switch (buffer[index++])
                    {
                        case 'c':
                        case 'C':
                            switch (buffer[index++])
                            {
                                case 'o':
                                case 'O':
                                    switch (buffer[index++])
                                    {
                                        case 'm':
                                        case 'M':
                                            switch (buffer[index++])
                                            {
                                                case 'p':
                                                case 'P':
                                                    switch (buffer[index++])
                                                    {
                                                        case 'a':
                                                        case 'A':
                                                            switch (buffer[index++])
                                                            {
                                                                case 'r':
                                                                case 'R':
                                                                    switch (buffer[index++])
                                                                    {
                                                                        case 'e':
                                                                        case 'E':
                                                                            switch (buffer[index++])
                                                                            {
                                                                                case '_':
                                                                                    switch (buffer[index++])
                                                                                    {
                                                                                        case 'b':
                                                                                        case 'B':
                                                                                            switch (buffer[index++])
                                                                                            {
                                                                                                case 's':
                                                                                                case 'S':
                                                                                                    switch (buffer[index++])
                                                                                                    {
                                                                                                        case 'o':
                                                                                                        case 'O':
                                                                                                            switch (buffer[index++])
                                                                                                            {
                                                                                                                case 'n':
                                                                                                                case 'N':
                                                                                                                    switch (buffer[index++])
                                                                                                                    {
                                                                                                                        case '_':
                                                                                                                            switch (buffer[index++])
                                                                                                                            {
                                                                                                                                case 'b':
                                                                                                                                case 'B':
                                                                                                                                    switch (buffer[index++])
                                                                                                                                    {
                                                                                                                                        case 'i':
                                                                                                                                        case 'I':
                                                                                                                                            switch (buffer[index++])
                                                                                                                                            {
                                                                                                                                                case 'n':
                                                                                                                                                case 'N':
                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                    {
                                                                                                                                                        case 'a':
                                                                                                                                                        case 'A':
                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                            {
                                                                                                                                                                case 'r':
                                                                                                                                                                case 'R':
                                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                                    {
                                                                                                                                                                        case 'y':
                                                                                                                                                                        case 'Y':
                                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                                            {
                                                                                                                                                                                case 'd':
                                                                                                                                                                                case 'D':
                                                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                                                    {
                                                                                                                                                                                        case 'a':
                                                                                                                                                                                        case 'A':
                                                                                                                                                                                            switch (buffer[index++])
                                                                                                                                                                                            {
                                                                                                                                                                                                case 't':
                                                                                                                                                                                                case 'T':
                                                                                                                                                                                                    switch (buffer[index++])
                                                                                                                                                                                                    {
                                                                                                                                                                                                        case 'a':
                                                                                                                                                                                                        case 'A':
                                                                                                                                                                                                            token = StringToken._Compare_Bson_BinaryData;
                                                                                                                                                                                                            break;
                                                                                                                                                                                                    }
                                                                                                                                                                                                    break;
                                                                                                                                                                                            }
                                                                                                                                                                                            break;
                                                                                                                                                                                    }
                                                                                                                                                                                    break;
                                                                                                                                                                            }
                                                                                                                                                                            break;
                                                                                                                                                                    }
                                                                                                                                                                    break;
                                                                                                                                                            }
                                                                                                                                                            break;
                                                                                                                                                    }
                                                                                                                                                    break;
                                                                                                                                            }
                                                                                                                                            break;
                                                                                                                                    }
                                                                                                                                    break;
                                                                                                                            }
                                                                                                                            break;
                                                                                                                    }
                                                                                                                    break;
                                                                                                            }
                                                                                                            break;
                                                                                                    }
                                                                                                    break;
                                                                                            }
                                                                                            break;
                                                                                    }
                                                                                    break;
                                                                            }
                                                                            break;
                                                                    }
                                                                    break;
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
            }

            return token;
        }
    }
}
