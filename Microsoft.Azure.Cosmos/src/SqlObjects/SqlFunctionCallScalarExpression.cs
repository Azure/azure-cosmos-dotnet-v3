//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
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
        private static readonly ImmutableDictionary<string, SqlIdentifier> FunctionIdentifiers = new Dictionary<string, SqlIdentifier>(StringComparer.OrdinalIgnoreCase)
        {
            { Names.InternalCompareBsonBinaryData, Identifiers.InternalCompareBsonBinaryData },
            { Names.InternalCompareObjects, Identifiers.InternalCompareObjects },
            { Names.InternalEvalEq, Identifiers.InternalEvalEq },
            { Names.InternalEvalGt, Identifiers.InternalEvalGt },
            { Names.InternalEvalGte, Identifiers.InternalEvalGte },
            { Names.InternalEvalIn, Identifiers.InternalEvalIn },
            { Names.InternalEvalLt, Identifiers.InternalEvalLt },
            { Names.InternalEvalLte, Identifiers.InternalEvalLte },
            { Names.InternalEvalNeq, Identifiers.InternalEvalNeq },
            { Names.InternalEvalNin, Identifiers.InternalEvalNin },
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
            { Names.Binary, Identifiers.Binary },
            { Names.Float32, Identifiers.Float32 },
            { Names.Float64, Identifiers.Float64 },
            { Names.Guid, Identifiers.Guid },
            { Names.Int16, Identifiers.Int16 },
            { Names.Int32, Identifiers.Int32 },
            { Names.Int64, Identifiers.Int64 },
            { Names.Int8, Identifiers.Int8 },
            { Names.List, Identifiers.List },
            { Names.ListContains, Identifiers.ListContains },
            { Names.Map, Identifiers.Map },
            { Names.MapContains, Identifiers.MapContains },
            { Names.MapContainsKey, Identifiers.MapContainsKey },
            { Names.MapContainsValue, Identifiers.MapContainsValue },
            { Names.Set, Identifiers.Set },
            { Names.SetContains, Identifiers.SetContains },
            { Names.Tuple, Identifiers.Tuple },
            { Names.Udt, Identifiers.Udt },
            { Names.UInt32, Identifiers.UInt32 },
            { Names.Ceiling, Identifiers.Ceiling },
            { Names.Concat, Identifiers.Concat },
            { Names.Contains, Identifiers.Contains },
            { Names.Cos, Identifiers.Cos },
            { Names.Cot, Identifiers.Cot },
            { Names.Count, Identifiers.Count },
            { Names.DateTimeAdd, Identifiers.DateTimeAdd },
            { Names.DateTimeDiff, Identifiers.DateTimeDiff },
            { Names.DateTimeFromParts, Identifiers.DateTimeFromParts },
            { Names.DateTimePart, Identifiers.DateTimePart },
            { Names.DateTimeToTicks, Identifiers.DateTimeToTicks },
            { Names.DateTimeToTimestamp, Identifiers.DateTimeToTimestamp },
            { Names.Degrees, Identifiers.Degrees },
            { Names.Documentid, Identifiers.Documentid },
            { Names.Endswith, Identifiers.Endswith },
            { Names.Exp, Identifiers.Exp },
            { Names.Floor, Identifiers.Floor },
            { Names.GetCurrentDateTime, Identifiers.GetCurrentDateTime },
            { Names.GetCurrentTicks, Identifiers.GetCurrentTicks },
            { Names.GetCurrentTimestamp, Identifiers.GetCurrentTimestamp },
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
            { Names.ObjectToArray, Identifiers.ObjectToArray },
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
            { Names.StringEquals, Identifiers.StringEquals },
            { Names.StringToArray, Identifiers.StringToArray },
            { Names.StringToBoolean, Identifiers.StringToBoolean },
            { Names.StringToNull, Identifiers.StringToNull },
            { Names.StringToNumber, Identifiers.StringToNumber },
            { Names.StringToObject, Identifiers.StringToObject },
            { Names.Substring, Identifiers.Substring },
            { Names.Sum, Identifiers.Sum },
            { Names.Tan, Identifiers.Tan },
            { Names.TicksToDateTime, Identifiers.TicksToDateTime },
            { Names.TimestampToDateTime, Identifiers.TimestampToDateTime },
            { Names.ToString, Identifiers.ToString },
            { Names.Trim, Identifiers.Trim },
            { Names.Trunc, Identifiers.Trunc },
            { Names.Upper, Identifiers.Upper },
        }.ToImmutableDictionary();

        private SqlFunctionCallScalarExpression(
            SqlIdentifier name,
            bool isUdf,
            ImmutableArray<SqlScalarExpression> arguments)
        {
            foreach (SqlScalarExpression argument in arguments)
            {
                if (argument == null)
                {
                    throw new ArgumentNullException($"{nameof(arguments)} must not have null items.");
                }
            }

            this.Arguments = arguments;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.IsUdf = isUdf;
        }

        public SqlIdentifier Name { get; }

        public ImmutableArray<SqlScalarExpression> Arguments { get; }

        public bool IsUdf { get; }

        public static SqlFunctionCallScalarExpression Create(
            SqlIdentifier name,
            bool isUdf,
            params SqlScalarExpression[] arguments) => new SqlFunctionCallScalarExpression(name, isUdf, arguments.ToImmutableArray());

        public static SqlFunctionCallScalarExpression Create(
            SqlIdentifier name,
            bool isUdf,
            ImmutableArray<SqlScalarExpression> arguments) => new SqlFunctionCallScalarExpression(name, isUdf, arguments);

        public static SqlFunctionCallScalarExpression Create(
            string name,
            bool isUdf,
            params SqlScalarExpression[] arguments) => SqlFunctionCallScalarExpression.Create(name, isUdf, arguments.ToImmutableArray());

        public static SqlFunctionCallScalarExpression Create(
            string name,
            bool isUdf,
            ImmutableArray<SqlScalarExpression> arguments)
        {
            if (!SqlFunctionCallScalarExpression.FunctionIdentifiers.TryGetValue(name, out SqlIdentifier sqlIdentifier))
            {
                sqlIdentifier = SqlIdentifier.Create(name);
            }

            return SqlFunctionCallScalarExpression.Create(sqlIdentifier, isUdf, arguments);
        }

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            string name,
            params SqlScalarExpression[] arguments) => SqlFunctionCallScalarExpression.Create(name, isUdf: false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            string name,
            ImmutableArray<SqlScalarExpression> arguments) => SqlFunctionCallScalarExpression.Create(name, isUdf: false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            SqlIdentifier name,
            params SqlScalarExpression[] arguments) => SqlFunctionCallScalarExpression.Create(name, isUdf: false, arguments);

        public static SqlFunctionCallScalarExpression CreateBuiltin(
            SqlIdentifier name,
            ImmutableArray<SqlScalarExpression> arguments) => SqlFunctionCallScalarExpression.Create(name, isUdf: false, arguments);

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
            public const string InternalEvalEq = "_M_EVAL_EQ";
            public const string InternalEvalGt = "_M_EVAL_GT";
            public const string InternalEvalGte = "_M_EVAL_GTE";
            public const string InternalEvalIn = "_M_EVAL_IN";
            public const string InternalEvalLt = "_M_EVAL_LT";
            public const string InternalEvalLte = "_M_EVAL_LTE";
            public const string InternalEvalNeq = "_M_EVAL_NEQ";
            public const string InternalEvalNin = "_M_EVAL_NIN";
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
            public const string Binary = "C_BINARY";
            public const string Float32 = "C_FLOAT32";
            public const string Float64 = "C_FLOAT64";
            public const string Guid = "C_GUID";
            public const string Int16 = "C_INT16";
            public const string Int32 = "C_INT32";
            public const string Int64 = "C_INT64";
            public const string Int8 = "C_INT8";
            public const string List = "C_LIST";
            public const string ListContains = "C_LISTCONTAINS";
            public const string Map = "C_MAP";
            public const string MapContains = "C_MAPCONTAINS";
            public const string MapContainsKey = "C_MAPCONTAINSKEY";
            public const string MapContainsValue = "C_MAPCONTAINSVALUE";
            public const string Set = "C_SET";
            public const string SetContains = "C_SETCONTAINS";
            public const string Tuple = "C_TUPLE";
            public const string Udt = "C_UDT";
            public const string UInt32 = "C_UINT32";
            public const string Ceiling = "CEILING";
            public const string Concat = "CONCAT";
            public const string Contains = "CONTAINS";
            public const string Cos = "COS";
            public const string Cot = "COT";
            public const string Count = "COUNT";
            public const string DateTimeAdd = "DateTimeAdd";
            public const string DateTimeDiff = "DateTimeDiff";
            public const string DateTimeFromParts = "DateTimeFromParts";
            public const string DateTimePart = "DateTimePart";
            public const string DateTimeToTicks = "DateTimeToTicks";
            public const string DateTimeToTimestamp = "DateTimeToTimestamp";
            public const string Degrees = "DEGREES";
            public const string Documentid = "DOCUMENTID";
            public const string Endswith = "ENDSWITH";
            public const string Exp = "EXP";
            public const string Floor = "FLOOR";
            public const string GetCurrentDateTime = "GetCurrentDateTime";
            public const string GetCurrentTicks = "GetCurrentTicks";
            public const string GetCurrentTimestamp = "GetCurrentTimestamp";
            public const string IndexOf = "INDEX_OF";
            public const string IntAdd = "IntAdd";
            public const string IntBitwiseAnd = "IntBitwiseAnd";
            public const string IntBitwiseLeftShift = "IntBitwiseLeftShift";
            public const string IntBitwiseNot = "IntBitwiseNot";
            public const string IntBitwiseOr = "IntBitwiseOr";
            public const string IntBitwiseRightShift = "IntBitwiseRightShift";
            public const string IntBitwiseXor = "IntBitwiseXor";
            public const string IntDiv = "IntDiv";
            public const string IntMod = "IntMod";
            public const string IntMul = "IntMul";
            public const string IntSub = "IntSub";
            public const string IsArray = "IS_ARRAY";
            public const string IsBool = "IS_BOOL";
            public const string IsDefined = "IS_DEFINED";
            public const string IsFiniteNumber = "IS_FINITE_NUMBER";
            public const string IsInteger = "IS_INTEGER";
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
            public const string ObjectToArray = "ObjectToArray";
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
            public const string StringEquals = "StringEquals";
            public const string StringToArray = "StringToArray";
            public const string StringToBoolean = "StringToBoolean";
            public const string StringToNull = "StringToNull";
            public const string StringToNumber = "StringToNumber";
            public const string StringToObject = "StringToObject";
            public const string Substring = "SUBSTRING";
            public const string Sum = "SUM";
            public const string Tan = "TAN";
            public const string TicksToDateTime = "TicksToDateTime";
            public const string TimestampToDateTime = "TimestampToDateTime";
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
            public const string ToString = "ToString";
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
            public const string Trim = "TRIM";
            public const string Trunc = "TRUNC";
            public const string Upper = "UPPER";
        }

        public static class Identifiers
        {
            public static readonly SqlIdentifier InternalCompareBsonBinaryData = SqlIdentifier.Create(Names.InternalCompareBsonBinaryData);
            public static readonly SqlIdentifier InternalCompareObjects = SqlIdentifier.Create(Names.InternalCompareObjects);
            public static readonly SqlIdentifier InternalEvalEq = SqlIdentifier.Create(Names.InternalEvalEq);
            public static readonly SqlIdentifier InternalEvalGt = SqlIdentifier.Create(Names.InternalEvalGt);
            public static readonly SqlIdentifier InternalEvalGte = SqlIdentifier.Create(Names.InternalEvalGte);
            public static readonly SqlIdentifier InternalEvalIn = SqlIdentifier.Create(Names.InternalEvalIn);
            public static readonly SqlIdentifier InternalEvalLt = SqlIdentifier.Create(Names.InternalEvalLt);
            public static readonly SqlIdentifier InternalEvalLte = SqlIdentifier.Create(Names.InternalEvalLte);
            public static readonly SqlIdentifier InternalEvalNeq = SqlIdentifier.Create(Names.InternalEvalNeq);
            public static readonly SqlIdentifier InternalEvalNin = SqlIdentifier.Create(Names.InternalEvalNin);
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
            public static readonly SqlIdentifier Binary = SqlIdentifier.Create(Names.Binary);
            public static readonly SqlIdentifier Float32 = SqlIdentifier.Create(Names.Float32);
            public static readonly SqlIdentifier Float64 = SqlIdentifier.Create(Names.Float64);
            public static readonly SqlIdentifier Guid = SqlIdentifier.Create(Names.Guid);
            public static readonly SqlIdentifier Int16 = SqlIdentifier.Create(Names.Int16);
            public static readonly SqlIdentifier Int32 = SqlIdentifier.Create(Names.Int32);
            public static readonly SqlIdentifier Int64 = SqlIdentifier.Create(Names.Int64);
            public static readonly SqlIdentifier Int8 = SqlIdentifier.Create(Names.Int8);
            public static readonly SqlIdentifier List = SqlIdentifier.Create(Names.List);
            public static readonly SqlIdentifier ListContains = SqlIdentifier.Create(Names.ListContains);
            public static readonly SqlIdentifier Map = SqlIdentifier.Create(Names.Map);
            public static readonly SqlIdentifier MapContains = SqlIdentifier.Create(Names.MapContains);
            public static readonly SqlIdentifier MapContainsKey = SqlIdentifier.Create(Names.MapContainsKey);
            public static readonly SqlIdentifier MapContainsValue = SqlIdentifier.Create(Names.MapContainsValue);
            public static readonly SqlIdentifier Set = SqlIdentifier.Create(Names.Set);
            public static readonly SqlIdentifier SetContains = SqlIdentifier.Create(Names.SetContains);
            public static readonly SqlIdentifier Tuple = SqlIdentifier.Create(Names.Tuple);
            public static readonly SqlIdentifier Udt = SqlIdentifier.Create(Names.Udt);
            public static readonly SqlIdentifier UInt32 = SqlIdentifier.Create(Names.UInt32);
            public static readonly SqlIdentifier Ceiling = SqlIdentifier.Create(Names.Ceiling);
            public static readonly SqlIdentifier Concat = SqlIdentifier.Create(Names.Concat);
            public static readonly SqlIdentifier Contains = SqlIdentifier.Create(Names.Contains);
            public static readonly SqlIdentifier Cos = SqlIdentifier.Create(Names.Cos);
            public static readonly SqlIdentifier Cot = SqlIdentifier.Create(Names.Cot);
            public static readonly SqlIdentifier Count = SqlIdentifier.Create(Names.Count);
            public static readonly SqlIdentifier DateTimeAdd = SqlIdentifier.Create(Names.DateTimeAdd);
            public static readonly SqlIdentifier DateTimeDiff = SqlIdentifier.Create(Names.DateTimeDiff);
            public static readonly SqlIdentifier DateTimeFromParts = SqlIdentifier.Create(Names.DateTimeFromParts);
            public static readonly SqlIdentifier DateTimePart = SqlIdentifier.Create(Names.DateTimePart);
            public static readonly SqlIdentifier DateTimeToTicks = SqlIdentifier.Create(Names.DateTimeToTicks);
            public static readonly SqlIdentifier DateTimeToTimestamp = SqlIdentifier.Create(Names.DateTimeToTimestamp);
            public static readonly SqlIdentifier Degrees = SqlIdentifier.Create(Names.Degrees);
            public static readonly SqlIdentifier Documentid = SqlIdentifier.Create(Names.Documentid);
            public static readonly SqlIdentifier Endswith = SqlIdentifier.Create(Names.Endswith);
            public static readonly SqlIdentifier Exp = SqlIdentifier.Create(Names.Exp);
            public static readonly SqlIdentifier Floor = SqlIdentifier.Create(Names.Floor);
            public static readonly SqlIdentifier GetCurrentDateTime = SqlIdentifier.Create(Names.GetCurrentDateTime);
            public static readonly SqlIdentifier GetCurrentTicks = SqlIdentifier.Create(Names.GetCurrentTicks);
            public static readonly SqlIdentifier GetCurrentTimestamp = SqlIdentifier.Create(Names.GetCurrentTimestamp);
            public static readonly SqlIdentifier IndexOf = SqlIdentifier.Create(Names.IndexOf);
            public static readonly SqlIdentifier IntAdd = SqlIdentifier.Create(Names.IntAdd);
            public static readonly SqlIdentifier IntBitwiseAnd = SqlIdentifier.Create(Names.IntBitwiseAnd);
            public static readonly SqlIdentifier IntBitwiseLeftShift = SqlIdentifier.Create(Names.IntBitwiseLeftShift);
            public static readonly SqlIdentifier IntBitwiseNot = SqlIdentifier.Create(Names.IntBitwiseNot);
            public static readonly SqlIdentifier IntBitwiseOr = SqlIdentifier.Create(Names.IntBitwiseOr);
            public static readonly SqlIdentifier IntBitwiseRightShift = SqlIdentifier.Create(Names.IntBitwiseRightShift);
            public static readonly SqlIdentifier IntBitwiseXor = SqlIdentifier.Create(Names.IntBitwiseXor);
            public static readonly SqlIdentifier IntDiv = SqlIdentifier.Create(Names.IntDiv);
            public static readonly SqlIdentifier IntMod = SqlIdentifier.Create(Names.IntMod);
            public static readonly SqlIdentifier IntMul = SqlIdentifier.Create(Names.IntMul);
            public static readonly SqlIdentifier IntSub = SqlIdentifier.Create(Names.IntSub);
            public static readonly SqlIdentifier IsArray = SqlIdentifier.Create(Names.IsArray);
            public static readonly SqlIdentifier IsBool = SqlIdentifier.Create(Names.IsBool);
            public static readonly SqlIdentifier IsDefined = SqlIdentifier.Create(Names.IsDefined);
            public static readonly SqlIdentifier IsFiniteNumber = SqlIdentifier.Create(Names.IsFiniteNumber);
            public static readonly SqlIdentifier IsInteger = SqlIdentifier.Create(Names.IsInteger);
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
            public static readonly SqlIdentifier ObjectToArray = SqlIdentifier.Create(Names.ObjectToArray);
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
            public static readonly SqlIdentifier StringEquals = SqlIdentifier.Create(Names.StringEquals);
            public static readonly SqlIdentifier StringToArray = SqlIdentifier.Create(Names.StringToArray);
            public static readonly SqlIdentifier StringToBoolean = SqlIdentifier.Create(Names.StringToBoolean);
            public static readonly SqlIdentifier StringToNull = SqlIdentifier.Create(Names.StringToNull);
            public static readonly SqlIdentifier StringToNumber = SqlIdentifier.Create(Names.StringToNumber);
            public static readonly SqlIdentifier StringToObject = SqlIdentifier.Create(Names.StringToObject);
            public static readonly SqlIdentifier Substring = SqlIdentifier.Create(Names.Substring);
            public static readonly SqlIdentifier Sum = SqlIdentifier.Create(Names.Sum);
            public static readonly SqlIdentifier Tan = SqlIdentifier.Create(Names.Tan);
            public static readonly SqlIdentifier TicksToDateTime = SqlIdentifier.Create(Names.TicksToDateTime);
            public static readonly SqlIdentifier TimestampToDateTime = SqlIdentifier.Create(Names.TimestampToDateTime);
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
            public static readonly SqlIdentifier ToString = SqlIdentifier.Create(Names.ToString);
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
            public static readonly SqlIdentifier Trim = SqlIdentifier.Create(Names.Trim);
            public static readonly SqlIdentifier Trunc = SqlIdentifier.Create(Names.Trunc);
            public static readonly SqlIdentifier Upper = SqlIdentifier.Create(Names.Upper);
        }
    }
}
