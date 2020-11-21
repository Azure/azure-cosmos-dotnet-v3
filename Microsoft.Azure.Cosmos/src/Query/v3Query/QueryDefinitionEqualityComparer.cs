// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Custom comparer class to check equality for query definition. This class does not check for
    /// logical equivalence.
    /// </summary>
    internal sealed class QueryDefinitionEqualityComparer : IEqualityComparer<QueryDefinition>
    {
        public static readonly QueryDefinitionEqualityComparer Instance = new QueryDefinitionEqualityComparer();

        private QueryDefinitionEqualityComparer()
        {
        }

        /// <summary>
        /// Checks for equality of two QueryDefinitions. Two queries are considered equal if
        /// 1. They are the same object in memory
        /// 2. Their query text is exactly the same AND they provide the same parameter values.
        /// Following are NOT Equal: (SELECT * FROM c WHERE c.A= @param1 AND c.B=@param2 , param1=val1, param2=val2), (SELECT * FROM c WHERE c.B= @param2 AND c.A=@param1 , param1=val1, param2=val2)
        /// Following are Equal: (SELECT * FROM c WHERE c.A= @param1 AND c.B=@param2 , param1=val1, param2=val2), (SELECT * FROM c WHERE c.A= @param1 AND c.B=@param2 , param2=val2, param1=val1)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>Boolean representing the equality</returns>
        public bool Equals(QueryDefinition x, QueryDefinition y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (x.Parameters.Count != y.Parameters.Count)
            {
                return false;
            }

            if (!string.Equals(x.QueryText, y.QueryText, StringComparison.Ordinal))
            {
                return false;
            }

            return ParameterEquals(x.Parameters, y.Parameters);
        }

        /// <summary>
        /// Caculates a hashcode of QueryDefinition, ignoring order of sqlParameters.
        /// </summary>
        /// <returns>Hash code of the object.</returns>
        public int GetHashCode(QueryDefinition queryDefinition)
        {
            unchecked
            {
                int hashCode = 23;
                foreach (SqlParameter queryParameter in queryDefinition.Parameters)
                {
                    // xor is associative so we generate the same hash code if order of parameters is different
                    hashCode ^= queryParameter.GetHashCode();
                }

                hashCode = (hashCode * 16777619) + queryDefinition.QueryText.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Checks if two sets of parameters have the same values.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="otherParameters"></param>
        /// <returns>True if parameters have the same values.</returns>
        private static bool ParameterEquals(IReadOnlyList<SqlParameter> parameters, IReadOnlyList<SqlParameter> otherParameters)
        {
            if (parameters.Count != otherParameters.Count)
            {
                return false;
            }

            HashSet<SqlParameter> set1 = new HashSet<SqlParameter>(parameters);
            HashSet<SqlParameter> set2 = new HashSet<SqlParameter>(otherParameters);

            return set1.SetEquals(set2);
        }
    }
}