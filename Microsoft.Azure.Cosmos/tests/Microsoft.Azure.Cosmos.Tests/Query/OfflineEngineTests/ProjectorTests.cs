//-----------------------------------------------------------------------
// <copyright file="CosmosObjectEqualityComparerUnitTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using OfflineEngine;
    using SqlObjects;
    using VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// CosmosObjectEqualityComparerUnitTests Class.
    /// </summary>
    [TestClass]
    public class ProjectorTests
    {
        [TestMethod]
        [Owner("brchon")]
        public void SqlSelectStarSpecTest()
        {
            CosmosObject john = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("John")
            });

            CosmosObject johnWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = john
            });

            SqlSelectStarSpec star = SqlSelectStarSpec.Singleton;
            AssertEvaluation(john, star, johnWrapped);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlSelectListSpecTest()
        {
            CosmosObject john = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("John"),
                ["age"] = CosmosNumber64.Create(25)
            });

            CosmosObject johnWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = john
            });

            // { c.name, c.age }
            SqlScalarExpression cDotName = TestUtils.CreatePathExpression("c", "name");

            SqlScalarExpression cDotAge = TestUtils.CreatePathExpression("c", "age");

            SqlSelectListSpec listSpec = SqlSelectListSpec.Create(
                SqlSelectItem.Create(cDotName),
                SqlSelectItem.Create(cDotAge));
            AssertEvaluation(john, listSpec, johnWrapped);

            // { c.name AS nameAlias }
            CosmosObject johnAliased = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["nameAlias"] = CosmosString.Create("John"),
            });

            SqlSelectListSpec listSpecWithAlias = SqlSelectListSpec.Create(SqlSelectItem.Create(cDotName, SqlIdentifier.Create("nameAlias")));
            AssertEvaluation(johnAliased, listSpecWithAlias, johnWrapped);

            // { 3 + 5 }
            CosmosObject johnNonPropertyName = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["$1"] = CosmosNumber64.Create(8)
            });

            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));
            SqlBinaryScalarExpression fivePlusThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Add, five, three);

            SqlSelectListSpec listSpecNonMember = SqlSelectListSpec.Create(SqlSelectItem.Create(fivePlusThree));
            AssertEvaluation(johnNonPropertyName, listSpecNonMember);

            // { 3 + 5 AS Five Plus Three }
            CosmosObject johnNonPropertyNameAliased = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["Five Plus Three"] = CosmosNumber64.Create(8)
            });
            SqlSelectListSpec listSpecNonMemberAliased = SqlSelectListSpec.Create(SqlSelectItem.Create(fivePlusThree, SqlIdentifier.Create("Five Plus Three")));
            AssertEvaluation(johnNonPropertyNameAliased, listSpecNonMemberAliased);

            // { c.blah[0] }
            CosmosObject numberIndex = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["blah"] = CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        CosmosNumber64.Create(0),
                        CosmosNumber64.Create(1),
                        CosmosNumber64.Create(2)
                    })
            });

            CosmosObject numberIndexWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = numberIndex
            });

            CosmosObject numberIndexEval = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["$1"] = CosmosNumber64.Create(0)
            });

            SqlScalarExpression cDotBlah0 = TestUtils.CreatePathExpression("c", "blah", 0);
            SqlSelectListSpec numberIndexSpec = SqlSelectListSpec.Create(SqlSelectItem.Create(cDotBlah0));
            AssertEvaluation(numberIndexEval, numberIndexSpec, numberIndexWrapped);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlSelectValueSpecTest()
        {
            CosmosObject john = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("John"),
                ["age"] = CosmosNumber64.Create(25)
            });

            CosmosObject johnWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = john
            });

            SqlScalarExpression cDotName = TestUtils.CreatePathExpression("c", "name");

            SqlSelectValueSpec valueSpec = SqlSelectValueSpec.Create(cDotName);
            AssertEvaluation(CosmosString.Create("John"), valueSpec, johnWrapped);
        }

        private static void AssertEvaluation(CosmosElement expected, SqlSelectSpec sqlSelectSpec, CosmosElement document = null)
        {
            CosmosElement evaluation = sqlSelectSpec.Accept(Projector.Singleton, document);
            Assert.AreEqual(expected, evaluation);
        }
    }
}