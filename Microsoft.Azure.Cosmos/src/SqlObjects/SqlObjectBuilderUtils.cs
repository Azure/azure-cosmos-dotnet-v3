//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Collections.Generic;
    using System.Linq;

    internal static class SqlObjectBuilderUtils
    {
        public static SqlMemberIndexerScalarExpression CreateSqlMemberIndexerScalarExpression(
            SqlScalarExpression first,
            SqlScalarExpression second,
            params SqlScalarExpression[] everythingElse)
        {
            List<SqlScalarExpression> segments = new List<SqlScalarExpression>(2 + everythingElse.Length);
            segments.Add(first);
            segments.Add(second);
            segments.AddRange(everythingElse);

            SqlMemberIndexerScalarExpression rootExpression = SqlMemberIndexerScalarExpression.Create(first, second);
            foreach (SqlScalarExpression indexer in segments.Skip(2))
            {
                rootExpression = SqlMemberIndexerScalarExpression.Create(rootExpression, indexer);
            }

            return rootExpression;
        }
    }
}
