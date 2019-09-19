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
    /// Child SqlObjectTextSerializer class used when user provides parameterized query by passing
    /// object string dictionary in <see cref="Linq.CosmosLinqExtensions.ToQueryDefinition{T}(IQueryable{T}, IDictionary{object, string})"/>
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
            if (this.findSqlNumberLiteralInParams(sqlNumberLiteral.Value, out string paramStr))
            {
                this.writer.Write(paramStr);
            }
            else
            {
                // We have to use InvariantCulture due to number formatting.
                // "1234.1234" is correct while "1234,1234" is incorrect.
                if (sqlNumberLiteral.Value.IsDouble)
                {
                    string literalString = sqlNumberLiteral.Value.ToString(CultureInfo.InvariantCulture);
                    double literalValue = 0.0;
                    if (!sqlNumberLiteral.Value.IsNaN &&
                        !sqlNumberLiteral.Value.IsInfinity &&
                        (!double.TryParse(literalString, NumberStyles.Number, CultureInfo.InvariantCulture, out literalValue) ||
                        !Number64.ToDouble(sqlNumberLiteral.Value).Equals(literalValue)))
                    {
                        literalString = sqlNumberLiteral.Value.ToString("G17", CultureInfo.InvariantCulture);
                    }

                    this.writer.Write(literalString);
                }
                else
                {
                    this.writer.Write(sqlNumberLiteral.Value.ToString(CultureInfo.InvariantCulture));
                }
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
                this.writer.Write("\"");
                this.writer.Write(SqlObjectTextSerializer.GetEscapedString(sqlStringLiteral.Value));
                this.writer.Write("\"");
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
                this.writer.Write(sqlBooleanLiteral.Value ? "true" : "false");
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
                if (sqlObjectLiteral.isValueSerialized)
                {
                    this.writer.Write(sqlObjectLiteral.Value);
                }
                else
                {
                    this.writer.Write(JsonConvert.SerializeObject(sqlObjectLiteral.Value));
                }
            }
        }

        private bool findSqlNumberLiteralInParams(Number64 value, out string paramStr)
        {
            paramStr = null;
            foreach (object key in this.parameters.Keys)
            {
                SqlNumberLiteral sqlNumberLiteral = ExpressionToSql.GetSqlNumberLiteral(key);
                if (sqlNumberLiteral != null && sqlNumberLiteral.Value.Equals(value))
                {
                    paramStr = this.parameters[key];
                    return true;
                }
            }

            return false;
        }
    }
}
