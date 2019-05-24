//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary> 
    /// Performs bottom-up analysis to determine which nodes can possibly 
    /// be part of an evaluated sub-tree. 
    /// </summary>
    internal static class Nominator
    {
        public static HashSet<Expression> Nominate(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            NominatorVisitor visitor = new NominatorVisitor(fnCanBeEvaluated);
            return visitor.Nominate(expression);
        }

        private sealed class NominatorVisitor : ExpressionVisitor
        {
            private readonly Func<Expression, bool> fnCanBeEvaluated;
            private HashSet<Expression> candidates;
            private bool canBeEvaluated;

            public NominatorVisitor(Func<Expression, bool> fnCanBeEvaluated)
            {
                this.fnCanBeEvaluated = fnCanBeEvaluated;
            }

            public HashSet<Expression> Nominate(Expression expression)
            {
                this.candidates = new HashSet<Expression>();
                this.Visit(expression);
                return this.candidates;
            }

            public override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    bool lastCanBeEvaluated = this.canBeEvaluated;
                    this.canBeEvaluated = true;
                    base.Visit(expression);
                    if (this.canBeEvaluated)
                    {
                        this.canBeEvaluated = this.fnCanBeEvaluated(expression);
                        if (this.canBeEvaluated)
                        {
                            this.candidates.Add(expression);
                        }
                    }
                    this.canBeEvaluated &= lastCanBeEvaluated;
                }
                return expression;
            }
        }
    }
}
