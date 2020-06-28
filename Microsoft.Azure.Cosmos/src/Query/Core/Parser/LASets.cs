//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Template generated code from Antlr4BuildTasks.Template v 3.0

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Atn;
    using Antlr4.Runtime.Misc;

    internal class LASets
    {

        private readonly Dictionary<Pair<ATNState, int>, bool> visited = new Dictionary<Pair<ATNState, int>, bool>();
        private readonly bool logParse = false;
        private readonly bool logClosure = false;

        private Parser parser;
        private CommonTokenStream tokenStream;
        private List<IToken> input;
        private int cursor;
        private HashSet<ATNState> stopStates;
        private HashSet<ATNState> startStates;

        private class Edge
        {
            public ATNState from;
            public ATNState to;
            public ATNState follow;
            public TransitionType type;
            public IntervalSet label;
            public int indexAtTransition;
            public int index; // Where we are in parse at _to state.
        }

        public LASets()
        {
        }

        public IntervalSet Compute(Parser parser, CommonTokenStream token_stream, int line, int col)
        {
            this.input = new List<IToken>();
            this.parser = parser;
            this.tokenStream = token_stream;
            this.stopStates = new HashSet<ATNState>();
            foreach (ATNState s in parser.Atn.ruleToStopState.Select(t => parser.Atn.states[t.stateNumber]))
            {
                this.stopStates.Add(s);
            }
            this.startStates = new HashSet<ATNState>();
            foreach (ATNState s in parser.Atn.ruleToStartState.Select(t => parser.Atn.states[t.stateNumber]))
            {
                this.startStates.Add(s);
            }
            int currentIndex = this.tokenStream.Index;
            this.tokenStream.Seek(0);
            int offset = 1;
            while (true)
            {
                IToken token = this.tokenStream.LT(offset++);
                this.input.Add(token);
                this.cursor = token.TokenIndex;
                if (token.Type == TokenConstants.EOF)
                {
                    break;
                }
                if (token.Line >= line && token.Column >= col)
                {
                    break;
                }
            }
            this.tokenStream.Seek(currentIndex);

            List<List<Edge>> all_parses = this.EnterState(new Edge()
            {
                index = 0,
                indexAtTransition = 0,
                to = this.parser.Atn.states[0],
                type = TransitionType.EPSILON
            });
            // Remove last token on input.
            this.input.RemoveAt(this.input.Count - 1);
            // Eliminate all paths that don't consume all input.
            List<List<Edge>> temp = new List<List<Edge>>();
            if (all_parses != null)
            {
                foreach (List<Edge> p in all_parses)
                {
                    //System.Console.Error.WriteLine(PrintSingle(p));
                    if (this.Validate(p, this.input))
                    {
                        temp.Add(p);
                    }
                }
            }
            all_parses = temp;
            if (all_parses != null && this.logClosure)
            {
                foreach (List<Edge> p in all_parses)
                {
                    System.Console.Error.WriteLine("Path " + this.PrintSingle(p));
                }
            }
            IntervalSet result = new IntervalSet();
            if (all_parses != null)
            {
                foreach (List<Edge> p in all_parses)
                {
                    HashSet<ATNState> set = this.ComputeSingle(p);
                    if (this.logClosure)
                    {
                        System.Console.Error.WriteLine("All states for path "
                                                       + string.Join(" ", set.ToList()));
                    }

                    foreach (ATNState s in set)
                    {
                        foreach (Transition t in s.TransitionsArray)
                        {
                            switch (t.TransitionType)
                            {
                                case TransitionType.RULE:
                                    break;

                                case TransitionType.PREDICATE:
                                    break;

                                case TransitionType.WILDCARD:
                                    break;

                                default:
                                    if (!t.IsEpsilon)
                                    {
                                        result.AddAll(t.Label);
                                    }

                                    break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private bool CheckPredicate(PredicateTransition transition)
        {
            return transition.Predicate.Eval(this.parser, ParserRuleContext.EmptyContext);
        }

        private int entryValue;

        // Step to state and continue parsing input.
        // Returns a list of transitions leading to a state that accepts input.
        private List<List<Edge>> EnterState(Edge t)
        {
            int here = ++this.entryValue;
            int index_on_transition = t.indexAtTransition;
            int token_index = t.index;
            ATNState state = t.to;
            IToken input_token = this.input[token_index];

            if (this.logParse)
            {
                System.Console.Error.WriteLine("Entry " + here
                                    + " State " + state
                                    + " tokenIndex " + token_index
                                    + " " + input_token.Text);
            }

            // Upon reaching the cursor, return match.
            bool at_match = input_token.TokenIndex >= this.cursor;
            if (at_match)
            {
                if (this.logParse)
                {
                    System.Console.Error.Write("Entry " + here
                                         + " return ");
                }

                List<List<Edge>> res = new List<List<Edge>>() { new List<Edge>() { t } };
                if (this.logParse)
                {
                    string str = this.PrintResult(res);
                    System.Console.Error.WriteLine(str);
                }
                return res;
            }

            if (this.visited.ContainsKey(new Pair<ATNState, int>(state, token_index)))
            {
                return null;
            }

            this.visited[new Pair<ATNState, int>(state, token_index)] = true;

            List<List<Edge>> result = new List<List<Edge>>();

            if (this.stopStates.Contains(state))
            {
                if (this.logParse)
                {
                    System.Console.Error.Write("Entry " + here
                                              + " return ");
                }

                List<List<Edge>> res = new List<List<Edge>>() { new List<Edge>() { t } };
                if (this.logParse)
                {
                    string str = this.PrintResult(res);
                    System.Console.Error.WriteLine(str);
                }
                return res;
            }

            // Search all transitions from state.
            foreach (Transition transition in state.TransitionsArray)
            {
                List<List<Edge>> matches = null;
                switch (transition.TransitionType)
                {
                    case TransitionType.RULE:
                        {
                            RuleTransition rule = (RuleTransition)transition;
                            ATNState sub_state = rule.target;
                            matches = this.EnterState(new Edge()
                            {
                                from = state,
                                to = rule.target,
                                follow = rule.followState,
                                label = rule.Label,
                                type = rule.TransitionType,
                                index = token_index,
                                indexAtTransition = token_index
                            });
                            if (matches != null && matches.Count == 0)
                            {
                                throw new Exception();
                            }

                            if (matches != null)
                            {
                                List<List<Edge>> new_matches = new List<List<Edge>>();
                                foreach (List<Edge> match in matches)
                                {
                                    Edge f = match.First(); // "to" is possibly final state of submachine.
                                    Edge l = match.Last(); // "to" is start state of submachine.
                                    bool is_final = this.stopStates.Contains(f.to);
                                    bool is_at_caret = f.index >= this.cursor;
                                    if (!is_final)
                                    {
                                        new_matches.Add(match);
                                    }
                                    else
                                    {
                                        List<List<Edge>> xxx = this.EnterState(new Edge()
                                        {
                                            from = f.to,
                                            to = rule.followState,
                                            label = null,
                                            type = TransitionType.EPSILON,
                                            index = f.index,
                                            indexAtTransition = f.index
                                        });
                                        if (xxx != null && xxx.Count == 0)
                                        {
                                            throw new Exception();
                                        }

                                        if (xxx != null)
                                        {
                                            foreach (List<Edge> y in xxx)
                                            {
                                                List<Edge> copy = y.ToList();
                                                foreach (Edge q in match)
                                                {
                                                    copy.Add(q);
                                                }
                                                new_matches.Add(copy);
                                            }

                                        }
                                    }
                                }
                                matches = new_matches;
                            }
                        }
                        break;

                    case TransitionType.PREDICATE:
                        if (this.CheckPredicate((PredicateTransition)transition))
                        {
                            matches = this.EnterState(new Edge()
                            {
                                from = state,
                                to = transition.target,
                                label = transition.Label,
                                type = transition.TransitionType,
                                index = token_index,
                                indexAtTransition = token_index
                            });
                            if (matches != null && matches.Count == 0)
                            {
                                throw new Exception();
                            }
                        }
                        break;

                    case TransitionType.WILDCARD:
                        matches = this.EnterState(new Edge()
                        {
                            from = state,
                            to = transition.target,
                            label = transition.Label,
                            type = transition.TransitionType,
                            index = token_index + 1,
                            indexAtTransition = token_index
                        });
                        if (matches != null && matches.Count == 0)
                        {
                            throw new Exception();
                        }

                        break;

                    default:
                        if (transition.IsEpsilon)
                        {
                            matches = this.EnterState(new Edge()
                            {
                                from = state,
                                to = transition.target,
                                label = transition.Label,
                                type = transition.TransitionType,
                                index = token_index,
                                indexAtTransition = token_index
                            });
                            if (matches != null && matches.Count == 0)
                            {
                                throw new Exception();
                            }
                        }
                        else
                        {
                            IntervalSet set = transition.Label;
                            if (set != null && set.Count > 0)
                            {
                                if (transition.TransitionType == TransitionType.NOT_SET)
                                {
                                    set = set.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, this.parser.Atn.maxTokenType));
                                }

                                if (set.Contains(input_token.Type))
                                {
                                    matches = this.EnterState(new Edge()
                                    {
                                        from = state,
                                        to = transition.target,
                                        label = transition.Label,
                                        type = transition.TransitionType,
                                        index = token_index + 1,
                                        indexAtTransition = token_index
                                    });
                                    if (matches != null && matches.Count == 0)
                                    {
                                        throw new Exception();
                                    }
                                }
                            }
                        }
                        break;
                }

                if (matches != null)
                {
                    foreach (List<Edge> match in matches)
                    {
                        List<Edge> x = match.ToList();
                        if (t != null)
                        {
                            x.Add(t);
                            Edge prev = null;
                            foreach (Edge z in x)
                            {
                                ATNState ff = z.to;
                                if (prev != null)
                                {
                                    if (prev.from != ff)
                                    {
                                        System.Console.Error.WriteLine("Fail " + this.PrintSingle(x));
                                        Debug.Assert(false);
                                    }
                                }

                                prev = z;
                            }
                        }
                        result.Add(x);
                    }
                }
            }
            if (result.Count == 0)
            {
                return null;
            }

            if (this.logParse)
            {
                System.Console.Error.Write("Entry " + here
                                              + " return ");
                string str = this.PrintResult(result);
                System.Console.Error.WriteLine(str);
            }
            return result;
        }

        private HashSet<ATNState> closure(ATNState start)
        {
            if (start == null)
            {
                throw new Exception();
            }

            HashSet<ATNState> visited = new HashSet<ATNState>();
            Stack<ATNState> stack = new Stack<ATNState>();
            stack.Push(start);
            while (stack.Any())
            {
                ATNState state = stack.Pop();
                if (visited.Contains(state))
                {
                    continue;
                }

                visited.Add(state);
                foreach (Transition transition in state.TransitionsArray)
                {
                    switch (transition.TransitionType)
                    {
                        case TransitionType.RULE:
                            {
                                RuleTransition rule = (RuleTransition)transition;
                                ATNState sub_state = rule.target;
                                HashSet<ATNState> cl = this.closure(sub_state);
                                if (cl.Where(s => this.stopStates.Contains(s) && s.atn == sub_state.atn).Any())
                                {
                                    HashSet<ATNState> cl2 = this.closure(rule.followState);
                                    cl.UnionWith(cl2);
                                }
                                foreach (ATNState c in cl)
                                {
                                    visited.Add(c);
                                }
                            }
                            break;

                        case TransitionType.PREDICATE:
                            if (this.CheckPredicate((PredicateTransition)transition))
                            {
                                if (transition.target == null)
                                {
                                    throw new Exception();
                                }

                                stack.Push(transition.target);
                            }
                            break;

                        case TransitionType.WILDCARD:
                            break;

                        default:
                            if (transition.IsEpsilon)
                            {
                                if (transition.target == null)
                                {
                                    throw new Exception();
                                }

                                stack.Push(transition.target);
                            }
                            break;
                    }
                }
            }
            return visited;
        }

        private HashSet<ATNState> ComputeSingle(List<Edge> parse)
        {
            List<Edge> copy = parse.ToList();
            HashSet<ATNState> result = new HashSet<ATNState>();
            if (this.logClosure)
            {
                System.Console.Error.WriteLine("Computing closure for the following parse:");
                System.Console.Error.Write(this.PrintSingle(parse));
                System.Console.Error.WriteLine();
            }

            if (!copy.Any())
            {
                return result;
            }

            Edge last_transaction = copy.First();
            if (last_transaction == null)
            {
                return result;
            }

            ATNState current_state = last_transaction.to;
            if (current_state == null)
            {
                throw new Exception();
            }

            for (; ; )
            {
                if (this.logClosure)
                {
                    System.Console.Error.WriteLine("Getting closure of " + current_state.stateNumber);
                }
                HashSet<ATNState> c = this.closure(current_state);
                if (this.logClosure)
                {
                    System.Console.Error.WriteLine("closure " + string.Join(" ", c.Select(s => s.stateNumber)));
                }
                bool do_continue = false;
                ATN atn = current_state.atn;
                int rule = current_state.ruleIndex;
                RuleStartState start_state = atn.ruleToStartState[rule];
                RuleStopState stop_state = atn.ruleToStopState[rule];
                bool changed = false;
                foreach (ATNState s in c)
                {
                    if (result.Contains(s))
                    {
                        continue;
                    }

                    changed = true;
                    result.Add(s);
                    if (s == stop_state)
                    {
                        do_continue = true;
                    }
                }
                if (!changed)
                {
                    break;
                }

                if (!do_continue)
                {
                    break;
                }

                for (; ; )
                {
                    if (!copy.Any())
                    {
                        break;
                    }

                    copy.RemoveAt(0);
                    if (!copy.Any())
                    {
                        break;
                    }

                    last_transaction = copy.First();
                    if (start_state == last_transaction.from)
                    {
                        copy.RemoveAt(0);
                        if (!copy.Any())
                        {
                            break;
                        }

                        last_transaction = copy.First();
                        // Get follow state of rule-type transition.
                        ATNState from_state = last_transaction.from;
                        if (from_state == null)
                        {
                            break;
                        }

                        ATNState follow_state = last_transaction.follow;
                        current_state = follow_state;
                        if (current_state == null)
                        {
                            throw new Exception();
                        }

                        break;
                    }
                }
            }
            return result;
        }

        private bool Validate(List<Edge> parse, List<IToken> i)
        {
            List<Edge> q = parse.ToList();
            q.Reverse();
            List<IToken>.Enumerator ei = this.input.GetEnumerator();
            List<Edge>.Enumerator eq = q.GetEnumerator();
            bool fei = false;
            bool feq = false;
            for (; ; )
            {
                fei = ei.MoveNext();
                IToken v = ei.Current;
                if (!fei)
                {
                    break;
                }

                bool empty = true;
                for (; empty;)
                {
                    feq = eq.MoveNext();
                    if (!feq)
                    {
                        break;
                    }

                    Edge x = eq.Current;
                    switch (x.type)
                    {
                        case TransitionType.RULE:
                            empty = true;
                            break;
                        case TransitionType.PREDICATE:
                            empty = true;
                            break;
                        case TransitionType.ACTION:
                            empty = true;
                            break;
                        case TransitionType.ATOM:
                            empty = false;
                            break;
                        case TransitionType.EPSILON:
                            empty = true;
                            break;
                        case TransitionType.INVALID:
                            empty = true;
                            break;
                        case TransitionType.NOT_SET:
                            empty = false;
                            break;
                        case TransitionType.PRECEDENCE:
                            empty = true;
                            break;
                        case TransitionType.SET:
                            empty = false;
                            break;
                        case TransitionType.WILDCARD:
                            empty = false;
                            break;
                        default:
                            throw new Exception();
                    }
                }
                Edge w = eq.Current;
                if (w == null && v == null)
                {
                    return true;
                }
                else if (w == null)
                {
                    return false;
                }
                else if (v == null)
                {
                    return false;
                }

                switch (w.type)
                {
                    case TransitionType.ATOM:
                        {
                            IntervalSet set = w.label;
                            if (set != null && set.Count > 0)
                            {
                                if (!set.Contains(v.Type))
                                {
                                    return false;
                                }
                            }
                            break;
                        }

                    case TransitionType.NOT_SET:
                        {
                            IntervalSet set = w.label;
                            set = set.Complement(IntervalSet.Of(TokenConstants.MinUserTokenType, this.parser.Atn.maxTokenType));
                            if (set != null && set.Count > 0)
                            {
                                if (!set.Contains(v.Type))
                                {
                                    return false;
                                }
                            }
                            break;
                        }

                    case TransitionType.SET:
                        {
                            IntervalSet set = w.label;
                            if (set != null && set.Count > 0)
                            {
                                if (!set.Contains(v.Type))
                                {
                                    return false;
                                }
                            }
                            break;
                        }

                    case TransitionType.WILDCARD:
                        break;

                    default:
                        throw new Exception();
                }
            }
            return true;
        }

        private string PrintSingle(List<Edge> parse)
        {
            StringBuilder sb = new StringBuilder();
            List<Edge> q = parse.ToList();
            q.Reverse();
            foreach (Edge t in q)
            {
                string sym = string.Empty;
                switch (t.type)
                {
                    case TransitionType.ACTION:
                        sym = "on action (eps)";
                        break;
                    case TransitionType.ATOM:
                        sym = "on " + t.label.ToString() + " ('" + this.input[t.indexAtTransition].Text + "')";
                        break;
                    case TransitionType.EPSILON:
                        sym = "on eps";
                        break;
                    case TransitionType.INVALID:
                        sym = "invalid (eps)";
                        break;
                    case TransitionType.NOT_SET:
                        sym = "on not " + t.label.ToString();
                        break;
                    case TransitionType.PRECEDENCE:
                        sym = "on prec (eps)";
                        break;
                    case TransitionType.PREDICATE:
                        sym = "on pred (eps)";
                        break;
                    case TransitionType.RANGE:
                        sym = "on " + t.label.ToString() + " ('" + this.input[t.indexAtTransition].Text + "')";
                        break;
                    case TransitionType.RULE:
                        sym = "on " + this.parser.RuleNames[t.to.ruleIndex] + " (eps)";
                        break;
                    case TransitionType.SET:
                        sym = "on " + t.label.ToString() + " ('" + this.input[t.indexAtTransition].Text + "')";
                        break;
                    case TransitionType.WILDCARD:
                        sym = "on wildcard ('" + this.input[t.indexAtTransition].Text + "')";
                        break;
                    default:
                        break;
                }
                sb.Append(" / " + t.from + " => " + t.to + " " + sym);
            }
            return sb.ToString();
        }

        private string PrintResult(List<List<Edge>> all_parses)
        {
            StringBuilder sb = new StringBuilder();
            foreach (List<Edge> p in all_parses)
            {
                sb.Append("||| " + this.PrintSingle(p));
            }
            return sb.ToString();
        }
    }
}
