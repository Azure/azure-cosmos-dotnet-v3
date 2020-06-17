// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Template generated code from Antlr4BuildTasks.Template v 3.0

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    internal sealed class ErrorListener<S> : ConsoleErrorListener<S>
    {
        public bool hadError;
        private readonly Parser parser;
        private readonly Lexer lexer;
        private readonly CommonTokenStream tokenStream;
        private bool firstTime;

        public ErrorListener(Parser parser, Lexer lexer, CommonTokenStream token_stream)
        {
            this.parser = parser;
            this.lexer = lexer;
            this.tokenStream = token_stream;
            this.firstTime = true;
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, S offendingSymbol, int line,
            int col, string msg, RecognitionException e)
        {
            this.hadError = true;
            if (this.firstTime)
            {
                try
                {
                    this.firstTime = false;
                    LASets la_sets = new LASets();
                    IntervalSet set = la_sets.Compute(this.parser, this.tokenStream, line, col);
                    List<string> result = new List<string>();
                    foreach (int r in set.ToList())
                    {
                        string rule_name = this.parser.Vocabulary.GetSymbolicName(r);
                        result.Add(rule_name);
                    }
                    if (result.Any())
                    {
                        System.Console.Error.WriteLine("Parse error line/col " + line + "/" + col
                                                       + ", expecting "
                                                       + string.Join(", ", result));
                    }
                    else
                    {
                        base.SyntaxError(output, recognizer, offendingSymbol, line, col, msg, e);
                    }

                    return;
                }
                catch (Exception)
                {
                }
            }
            base.SyntaxError(output, recognizer, offendingSymbol, line, col, msg, e);
        }
    }
}