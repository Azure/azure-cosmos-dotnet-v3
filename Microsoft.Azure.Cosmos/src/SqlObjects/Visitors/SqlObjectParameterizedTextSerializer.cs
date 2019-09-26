//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// This class used when user provides parameterized query by passing
    /// object string dictionary in <see cref="Linq.CosmosLinqExtensions.ToQueryDefinition{T}(IQueryable{T}, IDictionary{object, string})"/>
    /// Number, String, Boolean and Object(non array) will be parametrized
    /// </summary>
    internal class SqlObjectParameterizedTextSerializer : SqlObjectTextSerializer
    {
        private IDictionary<object, string> parameters;

        public SqlObjectParameterizedTextSerializer(bool prettyPrint,
            IDictionary<object, string> parameters)
            : base(prettyPrint)
        {
            this.parameters = parameters ?? new Dictionary<object, string>();
        }

        public override void Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            if (this.findSqlNumberLiteralInParams(sqlNumberLiteral.Value, out string paramName))
            {
                this.writer.Write(paramName);
            }
            else
            {
                base.Visit(sqlNumberLiteral);
            }
        }

        public override void Visit(SqlStringLiteral sqlStringLiteral)
        {
            if (this.parameters.ContainsKey(sqlStringLiteral.Value))
            {
                this.writer.Write(this.parameters[sqlStringLiteral.Value]);
            }
            else
            {
                base.Visit(sqlStringLiteral);
            }
        }

        public override void Visit(SqlBooleanLiteral sqlBooleanLiteral)
        {
            if (this.parameters.ContainsKey(sqlBooleanLiteral.Value))
            {
                this.writer.Write(this.parameters[sqlBooleanLiteral.Value]);
            }
            else
            {
                base.Visit(sqlBooleanLiteral);
            }
        }

        public override void Visit(SqlObjectLiteral sqlObjectLiteral)
        {
            if (this.parameters.ContainsKey(sqlObjectLiteral.Value))
            {
                this.writer.Write(this.parameters[sqlObjectLiteral.Value]);
            }
            else
            {
                base.Visit(sqlObjectLiteral);
            }
        }

        private bool findSqlNumberLiteralInParams(Number64 value, out string paramName)
        {
            paramName = null;
            foreach (object key in this.parameters.Keys)
            {
                SqlNumberLiteral sqlNumberLiteral = ExpressionToSql.GetSqlNumberLiteral(key);
                if (sqlNumberLiteral != null && sqlNumberLiteral.Value.Equals(value))
                {
                    paramName = this.parameters[key];
                    return true;
                }
            }

            return false;
        }
    }
}
