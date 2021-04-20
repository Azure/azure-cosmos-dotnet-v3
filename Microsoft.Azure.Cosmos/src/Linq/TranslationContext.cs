//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using static Microsoft.Azure.Cosmos.Linq.ExpressionToSql;
    using static Microsoft.Azure.Cosmos.Linq.FromParameterBindings;

    /// <summary>
    /// Used by the Expression tree visitor.
    /// </summary>
    internal sealed class TranslationContext
    {
        /// <summary>
        /// Set of parameters in scope at any point; used to generate fresh parameter names if necessary.
        /// </summary>
        public HashSet<ParameterExpression> InScope;
        /// <summary>
        /// Query that is being assembled.
        /// </summary>
        public QueryUnderConstruction currentQuery;

        /// <summary>
        /// Dictionary for parameter name and value
        /// </summary>
        public IDictionary<object, string> parameters;

        /// <summary>
        /// If the FROM clause uses a parameter name, it will be substituted for the parameter used in 
        /// the lambda expressions for the WHERE and SELECT clauses.
        /// </summary>
        private ParameterSubstitution substitutions;
        /// <summary>
        /// We are currently visiting these methods.
        /// </summary>
        private List<MethodCallExpression> methodStack;
        /// <summary>
        /// Stack of parameters from lambdas currently in scope.
        /// </summary>
        private List<ParameterExpression> lambdaParametersStack;
        /// <summary>
        /// Stack of collection-valued inputs.
        /// </summary>
        private List<Collection> collectionStack;
        /// <summary>
        /// The stack of subquery binding information.
        /// </summary>
        private Stack<SubqueryBinding> subqueryBindingStack;

        public TranslationContext()
        {
            this.InScope = new HashSet<ParameterExpression>();
            this.substitutions = new ParameterSubstitution();
            this.methodStack = new List<MethodCallExpression>();
            this.lambdaParametersStack = new List<ParameterExpression>();
            this.collectionStack = new List<Collection>();
            this.currentQuery = new QueryUnderConstruction(this.GetGenFreshParameterFunc());
            this.subqueryBindingStack = new Stack<SubqueryBinding>();
        }

        public TranslationContext(CosmosLinqSerializerOptions linqSerializerOptions, IDictionary<object, string> parameters = null)
            : this()
        {
            this.linqSerializerOptions = linqSerializerOptions;
            this.parameters = parameters;
        }

        public CosmosLinqSerializerOptions linqSerializerOptions;

        public Expression LookupSubstitution(ParameterExpression parameter)
        {
            return this.substitutions.Lookup(parameter);
        }

        public ParameterExpression GenFreshParameter(Type parameterType, string baseParameterName)
        {
            return Utilities.NewParameter(baseParameterName, parameterType, this.InScope);
        }

        public Func<string, ParameterExpression> GetGenFreshParameterFunc()
        {
            return (paramName) => this.GenFreshParameter(typeof(object), paramName);
        }

        /// <summary>
        /// Called when visiting a lambda with one parameter.
        /// Binds this parameter with the last collection visited.
        /// </summary>
        /// <param name="parameter">New parameter.</param>
        /// <param name="shouldBeOnNewQuery">Indicate if the parameter should be in a new QueryUnderConstruction clause</param>
        public void PushParameter(ParameterExpression parameter, bool shouldBeOnNewQuery)
        {
            this.lambdaParametersStack.Add(parameter);

            Collection last = this.collectionStack[this.collectionStack.Count - 1];
            if (last.isOuter)
            {
                // substitute
                ParameterExpression inputParam = this.currentQuery.GetInputParameterInContext(shouldBeOnNewQuery);
                this.substitutions.AddSubstitution(parameter, inputParam);
            }
            else
            {
                this.currentQuery.Bind(parameter, last.inner);
            }
        }

        /// <summary>
        /// Remove a parameter from the stack.
        /// </summary>
        public void PopParameter()
        {
            ParameterExpression last = this.lambdaParametersStack[this.lambdaParametersStack.Count - 1];
            this.lambdaParametersStack.RemoveAt(this.lambdaParametersStack.Count - 1);
        }

        /// <summary>
        /// Called when visiting a new MethodCall.
        /// </summary>
        /// <param name="method">Method that is being visited.</param>
        public void PushMethod(MethodCallExpression method)
        {
            if (method == null)
            {
                throw new ArgumentNullException("method");
            }

            this.methodStack.Add(method);
        }

        /// <summary>
        /// Called when finished visiting a MethodCall.
        /// </summary>
        public void PopMethod()
        {
            this.methodStack.RemoveAt(this.methodStack.Count - 1);
        }

        /// <summary>
        /// Return the top method in the method stack
        /// This is used only to determine the parameter name that the user provides in the lamda expression
        /// for readability purpose.
        /// </summary>
        public MethodCallExpression PeekMethod()
        {
            return (this.methodStack.Count > 0) ?
                this.methodStack[this.methodStack.Count - 1] :
                null;
        }

        /// <summary>
        /// Called when visiting a LINQ Method call with the input collection of the method.
        /// </summary>
        /// <param name="collection">Collection that is the input to a LINQ method.</param>
        public void PushCollection(Collection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            this.collectionStack.Add(collection);
        }

        public void PopCollection()
        {
            this.collectionStack.RemoveAt(this.collectionStack.Count - 1);
        }

        /// <summary>
        /// Sets the parameter used to scan the input.
        /// </summary>
        /// <param name="type">Type of the input parameter.</param>
        /// <param name="name">Suggested name for the input parameter.</param>
        public ParameterExpression SetInputParameter(Type type, string name)
        {
            return this.currentQuery.fromParameters.SetInputParameter(type, name, this.InScope);
        }

        /// <summary>
        /// Sets the parameter used by the this.fromClause if it is not already set.
        /// </summary>
        /// <param name="parameter">Parameter to set for the FROM clause.</param>
        /// <param name="collection">Collection to bind parameter to.</param>
        public void SetFromParameter(ParameterExpression parameter, SqlCollection collection)
        {
            Binding binding = new Binding(parameter, collection, isInCollection: true);
            this.currentQuery.fromParameters.Add(binding);
        }

        /// <summary>
        /// Gets whether the context is currently in a Select method at top level or not.
        /// Used to determine if a paramter should be an input parameter.
        /// </summary>
        public bool IsInMainBranchSelect()
        {
            if (this.methodStack.Count == 0) return false;

            bool isPositive = true;
            string bottomMethod = this.methodStack[0].ToString();
            for (int i = 1; i < this.methodStack.Count; ++i)
            {
                string currentMethod = this.methodStack[i].ToString();
                if (!bottomMethod.StartsWith(currentMethod, StringComparison.Ordinal))
                {
                    isPositive = false;
                    break;
                }

                bottomMethod = currentMethod;
            }

            string topMethodName = this.methodStack[this.methodStack.Count - 1].Method.Name;
            return isPositive && (topMethodName.Equals(LinqMethods.Select) || topMethodName.Equals(LinqMethods.SelectMany));
        }

        public void PushSubqueryBinding(bool shouldBeOnNewQuery)
        {
            this.subqueryBindingStack.Push(new SubqueryBinding(shouldBeOnNewQuery));
        }

        public SubqueryBinding PopSubqueryBinding()
        {
            if (this.subqueryBindingStack.Count == 0)
            {
                throw new InvalidOperationException("Unexpected empty subquery binding stack.");
            }

            return this.subqueryBindingStack.Pop();
        }

        public SubqueryBinding CurrentSubqueryBinding
        {
            get
            {
                if (this.subqueryBindingStack.Count == 0)
                {
                    throw new InvalidOperationException("Unexpected empty subquery binding stack.");
                }

                return this.subqueryBindingStack.Peek();
            }
        }

        /// <summary>
        /// Create a new QueryUnderConstruction node if indicated as neccesary by the subquery binding 
        /// </summary>
        /// <returns>The current QueryUnderConstruction after the package query call if necessary</returns>
        public QueryUnderConstruction PackageCurrentQueryIfNeccessary()
        {
            if (this.CurrentSubqueryBinding.ShouldBeOnNewQuery)
            {
                this.currentQuery = this.currentQuery.PackageQuery(this.InScope);
                this.CurrentSubqueryBinding.ShouldBeOnNewQuery = false;
            }

            return this.currentQuery;
        }

        public class SubqueryBinding
        {
            public static SubqueryBinding EmptySubqueryBinding = new SubqueryBinding(false);

            /// <summary>
            /// Indicates if the current query should be on a new QueryUnderConstruction
            /// </summary>
            public bool ShouldBeOnNewQuery { get; set; }
            /// <summary>
            /// Indicates the new bindings that are introduced when visiting the subquery
            /// </summary>
            public List<Binding> NewBindings { get; private set; }

            public SubqueryBinding(bool shouldBeOnNewQuery)
            {
                this.ShouldBeOnNewQuery = shouldBeOnNewQuery;
                this.NewBindings = new List<Binding>();
            }

            /// <summary>
            /// Consume all the bindings
            /// </summary>
            /// <returns>All the current bindings</returns>
            /// <remarks>The binding list is reset after this operation.</remarks>
            public List<Binding> TakeBindings()
            {
                List<Binding> bindings = this.NewBindings;
                this.NewBindings = new List<Binding>();
                return bindings;
            }
        }
    }

    /// <summary>
    /// Maintains a map from parameters to expressions.
    /// </summary>
    internal sealed class ParameterSubstitution
    {
        // In DocDB SQL parameters are bound in the FROM clause
        // and used in the sequent WHERE and SELECT.
        // E.g. SELECT VALUE x + 2 FROM data x WHERE x > 2
        // In Linq they are bound in each clause separately.
        // E.g. data.Where(x => x > 2).Select(y => y + 2).
        // This class is used to rename parameters, so that the Linq program above generates 
        // the correct SQL program above (modulo alpha-renaming).

        private Dictionary<ParameterExpression, Expression> substitutionTable;

        public ParameterSubstitution()
        {
            this.substitutionTable = new Dictionary<ParameterExpression, Expression>();
        }

        public void AddSubstitution(ParameterExpression parameter, Expression with)
        {
            if (parameter == with)
            {
                throw new InvalidOperationException("Substitution with self attempted");
            }

            this.substitutionTable.Add(parameter, with);
        }

        public Expression Lookup(ParameterExpression parameter)
        {
            if (this.substitutionTable.ContainsKey(parameter))
            {
                return this.substitutionTable[parameter];
            }

            return null;
        }

        public const string InputParameterName = "root";
    }

    /// <summary>
    /// Bindings for a set of parameters used in a FROM expression.
    /// Each parameter is bound to a collection.
    /// </summary>
    internal sealed class FromParameterBindings
    {
        /// <summary>
        /// Binding for a single parameter.
        /// </summary>
        public sealed class Binding
        {
            /// <summary>
            /// Parameter defined by FROM clause
            /// </summary>
            public ParameterExpression Parameter;

            /// <summary>
            /// How parameter is defined (may be null).  
            /// </summary>
            public SqlCollection ParameterDefinition;

            /// <summary>
            /// If true this corresponds to the clause `Parameter IN ParameterDefinition'
            /// else this corresponds to the clause `ParameterDefinition Parameter'
            /// </summary>
            public bool IsInCollection;

            /// <summary>
            /// True if a binding should be an input paramter for the next transformation. 
            /// E.g. in Select(f -> f.Children).Select(), if the lambda's translation is
            /// a subquery SELECT VALUE ARRAY() with alias v0 then v0 should be the input of the second Select.
            /// </summary>
            public bool IsInputParameter;

            public Binding(ParameterExpression parameter, SqlCollection collection, bool isInCollection, bool isInputParameter = true)
            {
                this.ParameterDefinition = collection;
                this.Parameter = parameter;
                this.IsInCollection = isInCollection;
                this.IsInputParameter = isInputParameter;

                if (isInCollection && collection == null)
                {
                    throw new ArgumentNullException($"{nameof(collection)} cannot be null for in-collection parameter.");
                }
            }
        }

        /// <summary>
        /// The list of parameter definitions.  This will generate a FROM clause of the shape:
        /// FROM ParameterDefinitions[0] JOIN ParameterDefinitions[1] ... ParameterDefinitions[n]
        /// </summary>
        private List<Binding> ParameterDefinitions;

        /// <summary>
        /// Create empty parameter bindings.
        /// </summary>
        public FromParameterBindings()
        {
            this.ParameterDefinitions = new List<Binding>();
        }

        /// <summary>
        /// Sets the parameter which iterates over the outer collection.
        /// </summary> 
        /// <param name="parameterType">Parameter type.</param>
        /// <param name="parameterName">Hint for name.</param>
        /// <param name="inScope">List of parameter names currently in scope.</param>
        /// <returns>The name of the parameter which iterates over the outer collection.  
        /// If the name is already set it will return the existing name.</returns>
        public ParameterExpression SetInputParameter(Type parameterType, string parameterName, HashSet<ParameterExpression> inScope)
        {
            if (this.ParameterDefinitions.Count > 0)
            {
                throw new InvalidOperationException("First parameter already set");
            }

            ParameterExpression newParam = Expression.Parameter(parameterType, parameterName);
            inScope.Add(newParam);
            Binding def = new Binding(newParam, collection: null, isInCollection: false);
            this.ParameterDefinitions.Add(def);
            return newParam;
        }

        public void Add(Binding binding)
        {
            this.ParameterDefinitions.Add(binding);
        }

        public IEnumerable<Binding> GetBindings()
        {
            return this.ParameterDefinitions;
        }

        /// <summary>
        /// Get the input parameter.
        /// </summary>
        /// <returns>The input parameter.</returns>
        public ParameterExpression GetInputParameter()
        {
            int i = this.ParameterDefinitions.Count - 1;
            while (i > 0 && !this.ParameterDefinitions[i].IsInputParameter) i--;

            // always the first one to be set.
            return i >= 0 ? this.ParameterDefinitions[i].Parameter : null;
        }
    }

    /// <summary>
    /// There are two types of collections: outer and inner.
    /// </summary>
    internal sealed class Collection
    {
        public bool isOuter;
        public SqlCollection inner;
        public string Name;

        /// <summary>
        /// Creates an outer collection.
        /// </summary>
        public Collection(string name)
        {
            this.isOuter = true;
            this.Name = name; // currently the name is not used for anything
        }

        public Collection(SqlCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            this.isOuter = false;
            this.inner = collection;
        }
    }
}