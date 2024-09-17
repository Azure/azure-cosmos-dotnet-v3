//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    public class IsFeedRangePartOfTestHelper
    {
        // Context to hold the parsed values
        public class GherkinContext
        {
            public string ChildMin { get; set; } = "";

            public string ChildMax { get; set; } = "";
            
            public bool ChildMaxInclusive { get; set; } = false;

            public string ParentMin { get; set; } = "";
            
            public string ParentMax { get; set; } = "";
            
            public bool ParentMaxInclusive { get; set; } = false;

            public bool DoesFit { get; set; } = false;
        }

        // The Expression interface
        public interface IGherkinExpression
        {
            void Interpret(GherkinContext context, string line);
        }

        // Concrete Expressions
        public class ChildRangeStartExpression : IGherkinExpression
        {
            public void Interpret(GherkinContext context, string line)
            {
                Match match = Regex.Match(line, @"Given the child range starts from (.+)");
                if (match.Success)
                {
                    context.ChildMin = match.Groups[1].Value.Trim() == "a lower bound minimum" ? "" : match.Groups[1].Value.Trim();
                }
            }
        }

        public class ChildRangeEndExpression : IGherkinExpression
        {
            public void Interpret(GherkinContext context, string line)
            {
                Match match = Regex.Match(line, @"And the child range (ending just before|ending at) (.+)");
                if (match.Success)
                {
                    context.ChildMaxInclusive = match.Groups[1].Value == "ending at";
                    context.ChildMax = match.Groups[2].Value.Trim();
                }
            }
        }

        public class ParentRangeStartExpression : IGherkinExpression
        {
            public void Interpret(GherkinContext context, string line)
            {
                Match match = Regex.Match(line, @"And the parent range starts from (.+)");
                if (match.Success)
                {
                    context.ParentMin = match.Groups[1].Value.Trim() == "a lower bound minimum" ? "" : match.Groups[1].Value.Trim();
                }
            }
        }

        public class ParentRangeEndExpression : IGherkinExpression
        {
            public void Interpret(GherkinContext context, string line)
            {
                Match match = Regex.Match(line, @"And the parent range (ending just before|ending at) (.+)");
                if (match.Success)
                {
                    context.ParentMaxInclusive = match.Groups[1].Value == "ending at";
                    context.ParentMax = match.Groups[2].Value.Trim();
                }
            }
        }

        public class ComparisonExpression : IGherkinExpression
        {
            public void Interpret(GherkinContext context, string line)
            {
                Match match = Regex.Match(line, @"Then the child range (fits entirely within|does not fit within) the parent range");
                if (match.Success)
                {
                    context.DoesFit = match.Groups[1].Value == "fits entirely within";
                }
            }
        }

        // The GherkinInterpreter that uses all expressions
        public class GherkinInterpreter
        {
            private readonly List<IGherkinExpression> _expressions;

            public GherkinInterpreter()
            {
                // Register all the concrete expressions
                this._expressions = new List<IGherkinExpression>
                {
                    new ChildRangeStartExpression(),
                    new ChildRangeEndExpression(),
                    new ParentRangeStartExpression(),
                    new ParentRangeEndExpression(),
                    new ComparisonExpression()
                };
            }

            public object[] ParseGherkinToObjectArray(string gherkin)
            {
                GherkinContext context = new GherkinContext();
                string[] lines = gherkin.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    foreach (IGherkinExpression expression in this._expressions)
                    {
                        expression.Interpret(context, line.Trim());
                    }
                }

                // Return the object array after interpreting the entire Gherkin
                return new object[]
                {
                    context.ChildMin,
                    context.ChildMax,
                    context.ChildMaxInclusive,
                    context.ParentMin,
                    context.ParentMax,
                    context.ParentMaxInclusive,
                    context.DoesFit
                };
            }
        }
    }
}
