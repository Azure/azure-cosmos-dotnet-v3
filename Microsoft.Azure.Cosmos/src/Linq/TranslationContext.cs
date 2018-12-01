//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Sql;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

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
        internal sealed class Binding
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

            public Binding(ParameterExpression parameter, SqlCollection collection, bool isInCollection)
            {
                this.ParameterDefinition = collection;
                this.Parameter = parameter;
                this.IsInCollection = isInCollection;

                if (isInCollection && collection == null)
                {
                    throw new ArgumentNullException("def");
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
        /// <param name="parameterName">Hint for name.</param>
        /// <param name="parameterType">Parameter type.</param>
        /// <param name="inScope">List of parameter names currently in scope.</param>
        /// <returns>The name of the parameter which iterates over the outer collection.  
        /// If the name is already set it will return the existing name.</returns>
        public ParameterExpression SetInputParameter(Type parameterType, string parameterName, HashSet<ParameterExpression> inScope)
        {
            if (this.ParameterDefinitions.Count > 0)
            {
                throw new InvalidOperationException("First parameter already set");
            }

            var newParam = Utilities.NewParameter(parameterName, parameterType, inScope);
            inScope.Add(newParam);
            Binding def = new Binding(newParam, null, false);
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
            // always the first one to be set.
            return this.ParameterDefinitions[this.ParameterDefinitions.Count - 1].Parameter;
        }
    }

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
        /// Query that is being assembled.
        /// </summary>
        public QueryUnderConstruction currentQuery;

        public TranslationContext()
        {
            this.InScope = new HashSet<ParameterExpression>();
            this.substitutions = new ParameterSubstitution();
            this.methodStack = new List<MethodCallExpression>();
            this.lambdaParametersStack = new List<ParameterExpression>();
            this.collectionStack = new List<Collection>();
            this.currentQuery = new QueryUnderConstruction();
        }

        public Expression LookupSubstitution(ParameterExpression parameter)
        {
            return this.substitutions.Lookup(parameter);
        }

        public ParameterExpression GenFreshParameter(Type parameterType, string baseParameterName)
        {
            return Utilities.NewParameter(baseParameterName, parameterType, this.InScope);
        }
        
        /// <summary>
        /// Called when visiting a lambda with one parameter.
        /// Binds this parameter with the last collection visited.
        /// </summary>
        /// <param name="parameter">New parameter.</param>
        public void PushParameter(ParameterExpression parameter)
        {
            this.lambdaParametersStack.Add(parameter);
            this.InScope.Add(parameter);

            Collection last = this.collectionStack[this.collectionStack.Count - 1];
            if (last.isOuter) 
            {
                // substitute
                var inputParam = this.GetInputParameter();
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
            this.InScope.Remove(last);
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
        /// <param name="name">Suggested name for the input parameter.</param>
        /// <param name="type">Type of the input parameter.</param>
        public ParameterExpression SetInputParameter(Type type, string name)
        {
            return this.currentQuery.fromParameters.SetInputParameter(type, name, this.InScope);
        }

        public ParameterExpression GetInputParameter()
        {
            return this.currentQuery.fromParameters.GetInputParameter();
        }

        /// <summary>
        /// Sets the parameter used by the this.fromClause if it is not already set.
        /// </summary>
        /// <param name="parameter">Parameter to set for the FROM clause.</param>
        /// <param name="collection">Collection to bind parameter to.</param>
        public void SetFromParameter(ParameterExpression parameter, SqlCollection collection)
        {
            FromParameterBindings.Binding binding = new FromParameterBindings.Binding(parameter, collection, true);
            this.currentQuery.fromParameters.Add(binding);
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