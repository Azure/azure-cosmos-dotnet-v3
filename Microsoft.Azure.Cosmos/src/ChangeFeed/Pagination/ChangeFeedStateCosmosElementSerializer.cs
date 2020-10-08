// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal static class ChangeFeedStateCosmosElementSerializer
    {
        private const string TypePropertyName = "type";
        private const string ValuePropertyName = "value";

        private const string BeginningTypeValue = "beginning";
        private const string TimeTypeValue = "time";
        private const string ContinuationTypeValue = "continuation";
        private const string NowTypeValue = "now";

        public static TryCatch<ChangeFeedState> MonadicFromCosmosElement(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException(nameof(cosmosElement));
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                return TryCatch<ChangeFeedState>.FromException(
                    new FormatException(
                        $"expected change feed state to be an object: {cosmosElement}"));
            }

            if (!cosmosObject.TryGetValue(TypePropertyName, out CosmosString typePropertyValue))
            {
                return TryCatch<ChangeFeedState>.FromException(
                    new FormatException(
                        $"expected change feed state to have a string type property: {cosmosElement}"));
            }

            ChangeFeedState state;
            switch (typePropertyValue.Value)
            {
                case BeginningTypeValue:
                    state = ChangeFeedState.Beginning();
                    break;

                case TimeTypeValue:
                    {
                        if (!cosmosObject.TryGetValue(ValuePropertyName, out CosmosString valuePropertyValue))
                        {
                            return TryCatch<ChangeFeedState>.FromException(
                                new FormatException(
                                    $"expected change feed state to have a string value property: {cosmosElement}"));
                        }

                        if (!DateTime.TryParse(
                            valuePropertyValue.Value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces,
                            out DateTime utcStartTime))
                        {
                            return TryCatch<ChangeFeedState>.FromException(
                                new FormatException(
                                    $"failed to parse start time value: {cosmosElement}"));
                        }

                        state = ChangeFeedState.Time(utcStartTime);
                    }
                    break;

                case ContinuationTypeValue:
                    {
                        if (!cosmosObject.TryGetValue(ValuePropertyName, out CosmosString valuePropertyValue))
                        {
                            return TryCatch<ChangeFeedState>.FromException(
                                new FormatException(
                                    $"expected change feed state to have a string value property: {cosmosElement}"));
                        }

                        state = ChangeFeedState.Continuation(valuePropertyValue);
                    }
                    break;

                case NowTypeValue:
                    state = ChangeFeedState.Now();
                    break;

                default:
                    throw new InvalidOperationException();
            }

            return TryCatch<ChangeFeedState>.FromResult(state);
        }

        public static CosmosElement ToCosmosElement(ChangeFeedState changeFeedState)
        {
            if (changeFeedState == null)
            {
                throw new ArgumentNullException(nameof(changeFeedState));
            }

            return changeFeedState.Accept(ChangeFeedToCosmosElementVisitor.Singleton);
        }

        private sealed class ChangeFeedToCosmosElementVisitor : IChangeFeedStateTransformer<CosmosElement>
        {
            public static readonly ChangeFeedToCosmosElementVisitor Singleton = new ChangeFeedToCosmosElementVisitor();

            private ChangeFeedToCosmosElementVisitor()
            {
            }

            private static readonly CosmosElement BegininningSingleton = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { TypePropertyName, CosmosString.Create(BeginningTypeValue) }
                });

            private static readonly CosmosElement NowSingleton = CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { TypePropertyName, CosmosString.Create(NowTypeValue) }
                });

            private static readonly CosmosString TimeTypeValueSingleton = CosmosString.Create(TimeTypeValue);

            private static readonly CosmosString ContinuationTypeValueSingleton = CosmosString.Create(ContinuationTypeValue);

            public CosmosElement Transform(ChangeFeedStateBeginning changeFeedStateBeginning)
            {
                return BegininningSingleton;
            }

            public CosmosElement Transform(ChangeFeedStateTime changeFeedStateTime)
            {
                return CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { TypePropertyName, TimeTypeValueSingleton },
                        { 
                            ValuePropertyName, 
                            CosmosString.Create(changeFeedStateTime.StartTime.ToString(
                                "o", 
                                CultureInfo.InvariantCulture)) 
                        }
                    });
            }

            public CosmosElement Transform(ChangeFeedStateContinuation changeFeedStateContinuation)
            {
                return CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { TypePropertyName, ContinuationTypeValueSingleton },
                        { ValuePropertyName, changeFeedStateContinuation.ContinuationToken }
                    });
            }

            public CosmosElement Transform(ChangeFeedStateNow changeFeedStateNow)
            {
                return NowSingleton;
            }
        }
    }
}
