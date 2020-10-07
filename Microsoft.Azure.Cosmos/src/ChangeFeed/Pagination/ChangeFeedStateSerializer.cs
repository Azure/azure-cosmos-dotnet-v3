// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class ChangeFeedStateSerializer
    {
        private const string TypePropertyName = "type";
        private const string ValuePropertyName = "value";

        private const string BeginningTypeValueString = "beginning";
        private const string TimeTypeValueString = "time";
        private const string ContinuationTypeValueString = "continuation";
        private const string NowTypeValueString = "now";

        public static TryCatch<ChangeFeedState> MonadicParse(string corpus)
        {
            if (corpus == null)
            {
                throw new ArgumentNullException(nameof(corpus));
            }

            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(corpus));
            string type = default;
            string value = default;

            if (!jsonReader.Read())
            {
                return TryCatch<ChangeFeedState>.FromException(
                    new MalformedChangeFeedContinuationTokenException("change feed state must not be an empty string or whitespace"));
            }

            if (jsonReader.CurrentTokenType != JsonTokenType.BeginObject)
            {
                return TryCatch<ChangeFeedState>.FromException(
                    new MalformedChangeFeedContinuationTokenException(
                        $"change feed state must be an object: {corpus}"));
            }

            while (jsonReader.Read() && (jsonReader.CurrentTokenType != JsonTokenType.EndObject))
            {
                if (jsonReader.CurrentTokenType != JsonTokenType.FieldName)
                {
                    return TryCatch<ChangeFeedState>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            $"property name expected in change feed state: {corpus}"));
                }

                string propertyName = jsonReader.GetStringValue();

                if (propertyName == TypePropertyName)
                {
                    if (!jsonReader.Read())
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"type property value expected in change feed token: {corpus}"));
                    }

                    if (jsonReader.CurrentTokenType != JsonTokenType.String)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"type property value expected to be a string in change feed token: {corpus}"));
                    }

                    type = jsonReader.GetStringValue();
                }
                else if (propertyName == ValuePropertyName)
                {
                    if (!jsonReader.Read())
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"value property value expected in change feed token: {corpus}"));
                    }

                    if (jsonReader.CurrentTokenType != JsonTokenType.String)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"value property value expected to be a string in change feed token: {corpus}"));
                    }

                    value = jsonReader.GetStringValue();
                }
                else
                {
                    return TryCatch<ChangeFeedState>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            $"unknown property value in change feed token: {corpus}"));
                }
            }

            if (jsonReader.CurrentTokenType != JsonTokenType.EndObject)
            {
                return TryCatch<ChangeFeedState>.FromException(
                        new MalformedChangeFeedContinuationTokenException(
                            $"expected end object token in change feed token: {corpus}"));
            }

            ChangeFeedState state;
            switch (type)
            {
                case BeginningTypeValueString:
                    if (value != default)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"unexpected value for start from beginning change feed state: {corpus}"));
                    }

                    state = ChangeFeedState.Beginning();
                    break;

                case TimeTypeValueString:
                    if (value == default)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"start from time change feed state missing value: {corpus}"));
                    }

                    if (!DateTime.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces,
                        out DateTime utcStartTime))
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"failed to parse start time value: {corpus}"));
                    }

                    state = ChangeFeedState.Time(utcStartTime);
                    break;

                case ContinuationTypeValueString:
                    if (value == default)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"start from continuation change feed state missing value: {corpus}"));
                    }

                    state = ChangeFeedState.Continuation(value);
                    break;

                case NowTypeValueString:
                    if (value != default)
                    {
                        return TryCatch<ChangeFeedState>.FromException(
                            new MalformedChangeFeedContinuationTokenException(
                                $"unexpected value for start from now change feed state: {corpus}"));
                    }

                    state = ChangeFeedState.Now();
                    break;

                default:
                    throw new InvalidOperationException();
            }

            return TryCatch<ChangeFeedState>.FromResult(state);
        }

        public static string ToString(ChangeFeedState changeFeedState)
        {
            if (changeFeedState == null)
            {
                throw new ArgumentNullException(nameof(changeFeedState));
            }

            return changeFeedState.Accept(ChangeFeedToCosmosElementVisitor.Singleton);
        }

        private sealed class ChangeFeedToCosmosElementVisitor : IChangeFeedStateTransformer<string>
        {
            public static readonly ChangeFeedToCosmosElementVisitor Singleton = new ChangeFeedToCosmosElementVisitor();

            private ChangeFeedToCosmosElementVisitor()
            {
            }

            private static readonly string BegininningToStringSingleton = $@"
                {{
                    ""{TypePropertyName}"" : ""{BeginningTypeValueString}""
                }}";

            private static readonly string NowToStringSingleton = $@"
                {{
                    ""{TypePropertyName}"" : ""{NowTypeValueString}""
                }}";

            public string Transform(ChangeFeedStateBeginning changeFeedStateBeginning)
            {
                return BegininningToStringSingleton;
            }

            public string Transform(ChangeFeedStateTime changeFeedStateTime)
            {
                return $@"
                    {{
                        ""{TypePropertyName}"" : ""{TimeTypeValueString}"",  
                        ""{ValuePropertyName}"" : ""{changeFeedStateTime.StartTime.ToString("R", CultureInfo.InvariantCulture)}"" 
                    }}";
            }

            public string Transform(ChangeFeedStateContinuation changeFeedStateContinuation)
            {
                return $@"
                    {{
                        ""{TypePropertyName}"" : ""{TimeTypeValueString}"",  
                        ""{ValuePropertyName}"" : ""{changeFeedStateContinuation.ContinuationToken}"" 
                    }}";
            }

            public string Transform(ChangeFeedStateNow changeFeedStateNow)
            {
                return NowToStringSingleton;
            }
        }
    }
}
