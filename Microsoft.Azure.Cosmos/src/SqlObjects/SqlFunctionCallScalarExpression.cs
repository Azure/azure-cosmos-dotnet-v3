//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlFunctionCallScalarExpression : SqlScalarExpression
    {
        private static readonly Dictionary<string, SqlIdentifier> FunctionIdentifiers = new Dictionary<string, SqlIdentifier>(StringComparer.OrdinalIgnoreCase)
        {
            { Names.InternalCompareBsonBinaryData, Identifiers.InternalCompareBsonBinaryData },
            { Names.InternalCompareObjects, Identifiers.InternalCompareObjects },
            { Names.InternalObjectToArray, Identifiers.InternalObjectToArray },
            { Names.InternalProxyProjection, Identifiers.InternalProxyProjection },
            { Names.InternalRegexMatch, Identifiers.InternalRegexMatch },
            { Names.InternalStDistance, Identifiers.InternalStDistance },
            { Names.InternalStIntersects, Identifiers.InternalStIntersects },
            { Names.InternalStWithin, Identifiers.InternalStWithin },
            { Names.InternalTryArrayContains, Identifiers.InternalTryArrayContains },
            { Names.Abs, Identifiers.Abs },
            { Names.Acos, Identifiers.Acos },
            { Names.All, Identifiers.All },
            { Names.Any, Identifiers.Any },
            { Names.Array, Identifiers.Array },
            { Names.ArrayConcat, Identifiers.ArrayConcat },
            { Names.ArrayContains, Identifiers.ArrayContains },
            { Names.ArrayLength, Identifiers.ArrayLength },
            { Names.ArraySlice, Identifiers.ArraySlice },
            { Names.Asin, Identifiers.Asin },
            { Names.Atan, Identifiers.Atan },
            { Names.Atn2, Identifiers.Atn2 },
            { Names.Avg, Identifiers.Avg },
            { Names.Ceiling, Identifiers.Ceiling },
            { Names.Concat, Identifiers.Concat },
            { Names.Contains, Identifiers.Contains },
            { Names.Cos, Identifiers.Cos },
            { Names.Cot, Identifiers.Cot },
            { Names.Count, Identifiers.Count },
            { Names.Degrees, Identifiers.Degrees },
            { Names.Documentid, Identifiers.Documentid },
            { Names.Endswith, Identifiers.Endswith },
            { Names.Exp, Identifiers.Exp },
            { Names.Floor, Identifiers.Floor },
            { Names.IndexOf, Identifiers.IndexOf },
            { Names.IsArray, Identifiers.IsArray },
            { Names.IsBool, Identifiers.IsBool },
            { Names.IsDefined, Identifiers.IsDefined },
            { Names.IsFiniteNumber, Identifiers.IsFiniteNumber },
            { Names.IsNull, Identifiers.IsNull },
            { Names.IsNumber, Identifiers.IsNumber },
            { Names.IsObject, Identifiers.IsObject },
            { Names.IsPrimitive, Identifiers.IsPrimitive },
            { Names.IsString, Identifiers.IsString },
            { Names.Left, Identifiers.Left },
            { Names.Length, Identifiers.Length },
            { Names.Like, Identifiers.Like },
            { Names.Log, Identifiers.Log },
            { Names.Log10, Identifiers.Log10 },
            { Names.Lower, Identifiers.Lower },
            { Names.Ltrim, Identifiers.Ltrim },
            { Names.Max, Identifiers.Max },
            { Names.Min, Identifiers.Min },
            { Names.Pi, Identifiers.Pi },
            { Names.Power, Identifiers.Power },
            { Names.Radians, Identifiers.Radians },
            { Names.Rand, Identifiers.Rand },
            { Names.Replace, Identifiers.Replace },
            { Names.Replicate, Identifiers.Replicate },
            { Names.Reverse, Identifiers.Reverse },
            { Names.Right, Identifiers.Right },
            { Names.Round, Identifiers.Round },
            { Names.Rtrim, Identifiers.Rtrim },
            { Names.Sign, Identifiers.Sign },
            { Names.Sin, Identifiers.Sin },
            { Names.Sqrt, Identifiers.Sqrt },
            { Names.Square, Identifiers.Square },
            { Names.Startswith, Identifiers.Startswith },
            { Names.StDistance, Identifiers.StDistance },
            { Names.StIntersects, Identifiers.StIntersects },
            { Names.StIsvalid, Identifiers.StIsvalid },
            { Names.StIsvaliddetailed, Identifiers.StIsvaliddetailed },
            { Names.StWithin, Identifiers.StWithin },
            { Names.Substring, Identifiers.Substring },
            { Names.Sum, Identifiers.Sum },
            { Names.Tan, Identifiers.Tan },
            { Names.Trunc, Identifiers.Trunc },
            { Names.Upper, Identifiers.Upper },
        };

        private SqlFunctionCallScalarExpression(
            SqlIdentifier name,
            bool isUdf,
            IReadOnlyList<SqlScalarExpression> arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException($"{nameof(arguments)} must not be null.");
            }

            foreach (SqlScalarExpression argument in arguments)
            {
                if (argument == null)
                {
                    throw new ArgumentNullException($"{nameof(arguments)} must not have null items.");
                }
            }

            this.Arguments = new List<SqlScalarExpression>(arguments);
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.IsUdf = isUdf;
        }

        public SqlIdentifier Name { get; }

        public IReadOnlyList<SqlScalarExpression> Arguments { get; }

        public bool IsUdf { get; }

        public static SqlFunctionCallScalarExpression Create(
            SqlIdentifier name,
            bool isUdf,
            params SqlScalarExpression[] arguments) => new SqlFunctionCallScalarExpression(name, isUdf, arguments);

        public static SqlFunctionCallScalarExpression Create(
            SqlIdentifier name,
            bool isUdf,
            IReadOnlyList<SqlScalarExpression> arguments) => new SqlFunctionCallScalarExpression(name, isUdf, arguments);

        public static SqlFunctionCallScalarExpression Create(
            string name,
            bool isUdf,
            params SqlScalarExpression[] arguments)
        {
            if (!SqlFunctionCallScalarExpression.FunctionIdentifiers.TryGetValue(name, out SqlIdentifier sqlIdentifier))
            {
                sqlIdentifier = SqlIdentifier.Create(name);
            }

            return SqlFunctionCallScalarExpression.Create(sqlIdentifier, isUdf, arguments);
        }

        public static SqlFunctionCallScalarExpression Create(
            string name,
            bool isUdf,
            IReadOnlyList<SqlScalarExpression> arguments)
        {
            if (!SqlFunctionCallScalarExpression.FunctionIdentifiers.TryGetValue(name, out SqlIdentifier sqlIdentifier))
            {
                sqlIdentifier = SqlIdentifier.Create(name);
            }

            return SqlFunctionCallScalarExpression.Create(sqlIdentifier, isUdf, arguments);
        }

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            string name,
            IReadOnlyList<SqlScalarExpression> arguments) => SqlFunctionCallScalarExpression.Create(name, false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            string name,
            params SqlScalarExpression[] arguments) => SqlFunctionCallScalarExpression.Create(name, false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            SqlIdentifier name,
            IReadOnlyList<SqlScalarExpression> arguments) => SqlFunctionCallScalarExpression.Create(name, false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            SqlIdentifier name,
            params SqlScalarExpression[] arguments) => SqlFunctionCallScalarExpression.Create(name, false, arguments);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public static class Names
        {
            public const string InternalCompareBsonBinaryData = "_COMPARE_BSON_BINARYDATA";
            public const string InternalCompareObjects = "_COMPARE_OBJECTS";
            public const string InternalObjectToArray = "_ObjectToArray";
            public const string InternalProxyProjection = "_PROXY_PROJECTION";
            public const string InternalRegexMatch = "_REGEX_MATCH";
            public const string InternalStDistance = "_ST_DISTANCE";
            public const string InternalStIntersects = "_ST_INTERSECTS";
            public const string InternalStWithin = "_ST_WITHIN";
            public const string InternalTryArrayContains = "_TRY_ARRAY_CONTAINS";

            public const string Abs = "ABS";
            public const string Acos = "ACOS";
            public const string All = "ALL";
            public const string Any = "ANY";
            public const string Array = "ARRAY";
            public const string ArrayConcat = "ARRAY_CONCAT";
            public const string ArrayContains = "ARRAY_CONTAINS";
            public const string ArrayLength = "ARRAY_LENGTH";
            public const string ArraySlice = "ARRAY_SLICE";
            public const string Asin = "ASIN";
            public const string Atan = "ATAN";
            public const string Atn2 = "ATN2";
            public const string Avg = "AVG";
            public const string Ceiling = "CEILING";
            public const string Concat = "CONCAT";
            public const string Contains = "CONTAINS";
            public const string Cos = "COS";
            public const string Cot = "COT";
            public const string Count = "COUNT";
            public const string Degrees = "DEGREES";
            public const string Documentid = "DOCUMENTID";
            public const string Endswith = "ENDSWITH";
            public const string Exp = "EXP";
            public const string Floor = "FLOOR";
            public const string IndexOf = "INDEX_OF";
            public const string IsArray = "IS_ARRAY";
            public const string IsBool = "IS_BOOL";
            public const string IsDefined = "IS_DEFINED";
            public const string IsFiniteNumber = "IS_FINITE_NUMBER";
            public const string IsNull = "IS_NULL";
            public const string IsNumber = "IS_NUMBER";
            public const string IsObject = "IS_OBJECT";
            public const string IsPrimitive = "IS_PRIMITIVE";
            public const string IsString = "IS_STRING";
            public const string Left = "LEFT";
            public const string Length = "LENGTH";
            public const string Like = "LIKE";
            public const string Log = "LOG";
            public const string Log10 = "LOG10";
            public const string Lower = "LOWER";
            public const string Ltrim = "LTRIM";
            public const string Max = "MAX";
            public const string Min = "MIN";
            public const string Pi = "PI";
            public const string Power = "POWER";
            public const string Radians = "RADIANS";
            public const string Rand = "RAND";
            public const string Replace = "REPLACE";
            public const string Replicate = "REPLICATE";
            public const string Reverse = "REVERSE";
            public const string Right = "RIGHT";
            public const string Round = "ROUND";
            public const string Rtrim = "RTRIM";
            public const string Sign = "SIGN";
            public const string Sin = "SIN";
            public const string Sqrt = "SQRT";
            public const string Square = "SQUARE";
            public const string Startswith = "STARTSWITH";
            public const string StDistance = "ST_DISTANCE";
            public const string StIntersects = "ST_INTERSECTS";
            public const string StIsvalid = "ST_ISVALID";
            public const string StIsvaliddetailed = "ST_ISVALIDDETAILED";
            public const string StWithin = "ST_WITHIN";
            public const string Substring = "SUBSTRING";
            public const string Sum = "SUM";
            public const string Tan = "TAN";
            public const string Trunc = "TRUNC";
            public const string Upper = "UPPER";
        }

        public static class Identifiers
        {
            public static readonly SqlIdentifier InternalCompareBsonBinaryData = SqlIdentifier.Create(Names.InternalCompareBsonBinaryData);
            public static readonly SqlIdentifier InternalCompareObjects = SqlIdentifier.Create(Names.InternalCompareObjects);
            public static readonly SqlIdentifier InternalObjectToArray = SqlIdentifier.Create(Names.InternalObjectToArray);
            public static readonly SqlIdentifier InternalProxyProjection = SqlIdentifier.Create(Names.InternalProxyProjection);
            public static readonly SqlIdentifier InternalRegexMatch = SqlIdentifier.Create(Names.InternalRegexMatch);
            public static readonly SqlIdentifier InternalStDistance = SqlIdentifier.Create(Names.InternalStDistance);
            public static readonly SqlIdentifier InternalStIntersects = SqlIdentifier.Create(Names.InternalStIntersects);
            public static readonly SqlIdentifier InternalStWithin = SqlIdentifier.Create(Names.InternalStWithin);
            public static readonly SqlIdentifier InternalTryArrayContains = SqlIdentifier.Create(Names.InternalTryArrayContains);

            public static readonly SqlIdentifier Abs = SqlIdentifier.Create(Names.Abs);
            public static readonly SqlIdentifier Acos = SqlIdentifier.Create(Names.Acos);
            public static readonly SqlIdentifier All = SqlIdentifier.Create(Names.All);
            public static readonly SqlIdentifier Any = SqlIdentifier.Create(Names.Any);
            public static readonly SqlIdentifier Array = SqlIdentifier.Create(Names.Array);
            public static readonly SqlIdentifier ArrayConcat = SqlIdentifier.Create(Names.ArrayConcat);
            public static readonly SqlIdentifier ArrayContains = SqlIdentifier.Create(Names.ArrayContains);
            public static readonly SqlIdentifier ArrayLength = SqlIdentifier.Create(Names.ArrayLength);
            public static readonly SqlIdentifier ArraySlice = SqlIdentifier.Create(Names.ArraySlice);
            public static readonly SqlIdentifier Asin = SqlIdentifier.Create(Names.Asin);
            public static readonly SqlIdentifier Atan = SqlIdentifier.Create(Names.Atan);
            public static readonly SqlIdentifier Atn2 = SqlIdentifier.Create(Names.Atn2);
            public static readonly SqlIdentifier Avg = SqlIdentifier.Create(Names.Avg);
            public static readonly SqlIdentifier Ceiling = SqlIdentifier.Create(Names.Ceiling);
            public static readonly SqlIdentifier Concat = SqlIdentifier.Create(Names.Concat);
            public static readonly SqlIdentifier Contains = SqlIdentifier.Create(Names.Contains);
            public static readonly SqlIdentifier Cos = SqlIdentifier.Create(Names.Cos);
            public static readonly SqlIdentifier Cot = SqlIdentifier.Create(Names.Cot);
            public static readonly SqlIdentifier Count = SqlIdentifier.Create(Names.Count);
            public static readonly SqlIdentifier Degrees = SqlIdentifier.Create(Names.Degrees);
            public static readonly SqlIdentifier Documentid = SqlIdentifier.Create(Names.Documentid);
            public static readonly SqlIdentifier Endswith = SqlIdentifier.Create(Names.Endswith);
            public static readonly SqlIdentifier Exp = SqlIdentifier.Create(Names.Exp);
            public static readonly SqlIdentifier Floor = SqlIdentifier.Create(Names.Floor);
            public static readonly SqlIdentifier IndexOf = SqlIdentifier.Create(Names.IndexOf);
            public static readonly SqlIdentifier IsArray = SqlIdentifier.Create(Names.IsArray);
            public static readonly SqlIdentifier IsBool = SqlIdentifier.Create(Names.IsBool);
            public static readonly SqlIdentifier IsDefined = SqlIdentifier.Create(Names.IsDefined);
            public static readonly SqlIdentifier IsFiniteNumber = SqlIdentifier.Create(Names.IsFiniteNumber);
            public static readonly SqlIdentifier IsNull = SqlIdentifier.Create(Names.IsNull);
            public static readonly SqlIdentifier IsNumber = SqlIdentifier.Create(Names.IsNumber);
            public static readonly SqlIdentifier IsObject = SqlIdentifier.Create(Names.IsObject);
            public static readonly SqlIdentifier IsPrimitive = SqlIdentifier.Create(Names.IsPrimitive);
            public static readonly SqlIdentifier IsString = SqlIdentifier.Create(Names.IsString);
            public static readonly SqlIdentifier Left = SqlIdentifier.Create(Names.Left);
            public static readonly SqlIdentifier Length = SqlIdentifier.Create(Names.Length);
            public static readonly SqlIdentifier Like = SqlIdentifier.Create(Names.Like);
            public static readonly SqlIdentifier Log = SqlIdentifier.Create(Names.Log);
            public static readonly SqlIdentifier Log10 = SqlIdentifier.Create(Names.Log10);
            public static readonly SqlIdentifier Lower = SqlIdentifier.Create(Names.Lower);
            public static readonly SqlIdentifier Ltrim = SqlIdentifier.Create(Names.Ltrim);
            public static readonly SqlIdentifier Max = SqlIdentifier.Create(Names.Max);
            public static readonly SqlIdentifier Min = SqlIdentifier.Create(Names.Min);
            public static readonly SqlIdentifier Pi = SqlIdentifier.Create(Names.Pi);
            public static readonly SqlIdentifier Power = SqlIdentifier.Create(Names.Power);
            public static readonly SqlIdentifier Radians = SqlIdentifier.Create(Names.Radians);
            public static readonly SqlIdentifier Rand = SqlIdentifier.Create(Names.Rand);
            public static readonly SqlIdentifier Replace = SqlIdentifier.Create(Names.Replace);
            public static readonly SqlIdentifier Replicate = SqlIdentifier.Create(Names.Replicate);
            public static readonly SqlIdentifier Reverse = SqlIdentifier.Create(Names.Reverse);
            public static readonly SqlIdentifier Right = SqlIdentifier.Create(Names.Right);
            public static readonly SqlIdentifier Round = SqlIdentifier.Create(Names.Round);
            public static readonly SqlIdentifier Rtrim = SqlIdentifier.Create(Names.Rtrim);
            public static readonly SqlIdentifier Sign = SqlIdentifier.Create(Names.Sign);
            public static readonly SqlIdentifier Sin = SqlIdentifier.Create(Names.Sin);
            public static readonly SqlIdentifier Sqrt = SqlIdentifier.Create(Names.Sqrt);
            public static readonly SqlIdentifier Square = SqlIdentifier.Create(Names.Square);
            public static readonly SqlIdentifier Startswith = SqlIdentifier.Create(Names.Startswith);
            public static readonly SqlIdentifier StDistance = SqlIdentifier.Create(Names.StDistance);
            public static readonly SqlIdentifier StIntersects = SqlIdentifier.Create(Names.StIntersects);
            public static readonly SqlIdentifier StIsvalid = SqlIdentifier.Create(Names.StIsvalid);
            public static readonly SqlIdentifier StIsvaliddetailed = SqlIdentifier.Create(Names.StIsvaliddetailed);
            public static readonly SqlIdentifier StWithin = SqlIdentifier.Create(Names.StWithin);
            public static readonly SqlIdentifier Substring = SqlIdentifier.Create(Names.Substring);
            public static readonly SqlIdentifier Sum = SqlIdentifier.Create(Names.Sum);
            public static readonly SqlIdentifier Tan = SqlIdentifier.Create(Names.Tan);
            public static readonly SqlIdentifier Trunc = SqlIdentifier.Create(Names.Trunc);
            public static readonly SqlIdentifier Upper = SqlIdentifier.Create(Names.Upper);
        }
    }
}
