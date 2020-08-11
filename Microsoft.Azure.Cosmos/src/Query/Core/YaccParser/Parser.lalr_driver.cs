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
        private const int MaxAllowedNestingDepth = 3400;

        private const int YYSTACKSIZE = 1000;
        private const int YYEMPTY = -1;

        private const int YYERRCODE = 256;
        private const int DEEPNESTING = 1023;
        private const int STACKOFLOW = 1024;

        private static SqlProgram yyparse(Parser parser)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            int yym, yyn;
            short yystate, yynewstate;

            YYSTYPE yylval, yyval = default;
            Stack<YYSTYPE> yyvsp = new Stack<YYSTYPE>(); // value stack
            YYLTYPE yylloc = default;
            YYLTYPE yyloc = default;
            Stack<YYLTYPE> yylsp = new Stack<YYLTYPE>(); // location stack
            int yynerrs, yyerrflag, yychar;
            Stack<short> yyssp = new Stack<short>(); // state stack

            yynerrs = 0;
            yyerrflag = 0;
            yychar = YYEMPTY;

            yyssp.Push(yystate = 0);

yyloop:
            if ((yyn = y_tab.yydefred[yystate]) != 0) goto yyreduce;

            if (yychar < 0)
            {
                if (!parser.TryGetNextToken(out yychar, out yylval, out yylloc))
                {
                    goto yyabort;
                }

                if (yychar < 0)
                {
                    // This indicates a scanner unexpected error (SCANNER_ERROR)
                    goto yyabort;
                }
            }

            if (((yyn = y_tab.yysindex[yystate]) > 0)
                && (yyn += yychar) >= 0
                && yyn <= y_tab.SQLTABLESIZE
                && y_tab.yycheck[yyn] == yychar)
            {
                if (yyssp.Count >= YYSTACKSIZE)
                {
                    goto yyoverflow;
                }

                yynewstate = y_tab.yytable[yyn];

                if (yynewstate < 0)
                {
                    yyn = y_tab.yytransform(parser, yychar, yylval, -yynewstate);

                    if (yyn == yychar)
                        goto yyerrlab;

                    yychar = yyn;

                    // Now we need to check the validity of the transformed token again, and if not valid, proceed directly to
                    // reporting the error, because reduce action is not possible.
                    if (((yyn = y_tab.yysindex[yystate]) > 0)
                        && (yyn += yychar) >= 0
                        && yyn <= y_tab.SQLTABLESIZE
                        && y_tab.yycheck[yyn] == yychar)
                        yynewstate = y_tab.yytable[yyn];
                    else
                        goto report_error;
                }

                yyssp.Push(yystate = yynewstate);
                yyvsp.Push(yylval);
                yylsp.Push(yylloc);
                yychar = YYEMPTY;
                if (yyerrflag > 0) --yyerrflag;
                goto yyloop;
            }
            if (((yyn = y_tab.yyrindex[yystate]) > 0) &&
                (yyn += yychar) >= 0 &&
                yyn <= y_tab.SQLTABLESIZE &&
                y_tab.yycheck[yyn] == yychar)
            {
                yyn = y_tab.yytable[yyn];
                goto yyreduce;
            }
report_error:
            if (yyerrflag > 0) goto yyinrecovery;

            yyerror(parser, yychar, yylloc);

yyerrlab:
            ++yynerrs;

yyinrecovery:
            if (yyerrflag < 3)
            {
                yyerrflag = 3;
                for (; ; )
                {
                    if (
                        ((yyn = y_tab.yysindex[yyssp.Peek()]) > 0) &&
                        (yyn += YYERRCODE) >= 0 &&
                        yyn <= y_tab.SQLTABLESIZE
                        && y_tab.yycheck[yyn] == YYERRCODE)
                    {
                        if (yyssp.Count >= YYSTACKSIZE)
                        {
                            goto yyoverflow;
                        }

                        yyssp.Push(yystate = y_tab.yytable[yyn]);
                        yyvsp.Push(yylval);
                        yylsp.Push(yylloc);
                        goto yyloop;
                    }
                    else
                    {
                        if (yyssp.Count == 0) goto yyabort;
                        yyssp.Pop();
                        yyvsp.Pop();
                        yylsp.Pop();
                    }
                }
            }
            else
            {
                if (yychar == 0) goto yyabort;
                yychar = YYEMPTY;
                goto yyloop;
            }

yyreduce:
            yym = y_tab.yylen[yyn];

            // Check for deeply nested objects
            if (yylloc.StartIndex > (MaxAllowedNestingDepth * 2))
            {
                if ((yyval.QueryObject != null) && (yyval.QueryObject is SqlBinaryScalarExpression binaryExpression))
                {
                    ulong nDepth = Parser.GetBinaryScalarExpressionDepth(binaryExpression);
                    if (nDepth > Parser.MaxAllowedNestingDepth) goto yydeepnesting;
                }
            }

            // NOTE: We here disable the default action {$$ = $1} and replace it by resetting $$. 
            // This is necessary since we almost always use $$ as an output parameter which needs 
            // to be reset first. This basically means that in sql.y we need to explicitly specify 
            // {$$ = $1} rule if needed (as opposed to omitting it).
            // BEFORE: yyval = yyvsp[1 - yym];
            yyval = default;
            yyloc = Merge(yylsp[1 - yym], yylsp[0]);

            switch (yyn)
            {
# include "y_code.cpp"
            }
            if (FAILED(hr)) goto yyabort;
            yyssp -= yym;
            yystate = yyssp.Peek();

            // After a reduction we clean the freed stack items
            for (int i = 0; i < yym - 1; i++)
            {
                *(yyvsp - i) = YYSTYPE();
                *(yylsp - i) = YYLTYPE();
            }

            yyvsp -= yym;
            yylsp -= yym;
            yym = yylhs[yyn];
            if (yystate == 0 && yym == 0)
            {
                yystate = SQLFINAL;
                *++yyssp = SQLFINAL;
                *++yyvsp = yyval;
                *++yylsp = yyloc;
                if (yychar < 0)
                {
                    hr = pQueryParser->GetNextToken(yychar, &yylval, &yylloc);
                    if (hr != S_OK) YYABORT;
                    if (yychar < 0)
                    {
                        // This indicates a scanner unexpected error (SCANNER_ERROR)
                        hr = E_FAIL;
                        YYABORT;
                    }

# ifdef _DEBUG
                    if (yydebug)
                    {
                        yys = 0;
                        if (yychar <= SQLMAXTOKEN) yys = yyname[yychar];
                        if (!yys) yys = "illegal-symbol";
                        traceprint(L"yydebug: state %d, reading %d (%hs)\n", SQLFINAL, yychar, yys);
                    }
#endif
                }
                if (yychar == 0) goto yyaccept;
                goto yyloop;
            }
            if ((yyn = yygindex[yym]) && (yyn += yystate) >= 0 &&
                yyn <= SQLTABLESIZE && yycheck[yyn] == yystate)
                yystate = yytable[yyn];
            else
                yystate = yydgoto[yym];
# ifdef _DEBUG
            if (yydebug)
                traceprint(L"yydebug: after reduction, shifting from state %d to state %d\n", *yyssp, yystate);
#endif
            if (yyssp >= yyss + yystacksize - 1)
            {
                goto yyoverflow;
            }
            *++yyssp = yystate;
            *++yyvsp = yyval;
            *++yylsp = yyloc;

            goto yyloop;

        }

        private static int yytransform(Parser parser, int yychar, YYSTYPE yylval, short context)
        {
            return 0;
        }

        private static void yyerror(Parser parser, int token, YYLTYPE location)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }

            switch (token)
            {
                case DEEPNESTING:
                case STACKOFLOW:
                    parser.RegisterErrroQueryTooComplex(location);
                    break;

                default:
                    parser.RegisterErrorIncorrectSyntax(location);
                    break;
            }
        }
    }
}
