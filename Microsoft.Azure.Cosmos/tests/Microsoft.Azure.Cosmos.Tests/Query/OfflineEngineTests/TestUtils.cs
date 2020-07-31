namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class TestUtils
    {
        public static SqlScalarExpression CreatePathExpression(params PathToken[] tokens)
        {
            Path path = new Path();
            foreach (PathToken token in tokens)
            {
                path.ExtendPath(token);
            }

            return GenerateMemberIndexerScalarExpressionFromPath(path);
        }

        private static SqlScalarExpression GenerateMemberIndexerScalarExpressionFromPath(Path path)
        {
            if (path.Length < 1)
            {
                throw new ArgumentException($"{nameof(path)} is too short.");
            }

            if (!(path.First() is StringPathToken rootToken))
            {
                throw new ArgumentException($"{nameof(path)} did not start with a string.");
            }

            SqlScalarExpression rootExpression = SqlPropertyRefScalarExpression.Create(
                member: null,
                identifier: SqlIdentifier.Create(rootToken.PropertyName));

            foreach (PathToken token in path.Skip(1))
            {
                SqlLiteralScalarExpression memberIndexer;
                switch (token)
                {
                    case StringPathToken stringPathToken:
                        memberIndexer = SqlLiteralScalarExpression.Create(
                            SqlStringLiteral.Create(
                                stringPathToken.PropertyName));
                        break;

                    case IntegerPathToken integerPathToken:
                        memberIndexer = SqlLiteralScalarExpression.Create(
                            SqlNumberLiteral.Create(
                                integerPathToken.Index));
                        break;

                    default:
                        throw new ArgumentException($"Unknown token type: {token.GetType()}; {token}");
                }

                rootExpression = SqlMemberIndexerScalarExpression.Create(rootExpression, memberIndexer);
            }

            return rootExpression;
        }
    }
}
