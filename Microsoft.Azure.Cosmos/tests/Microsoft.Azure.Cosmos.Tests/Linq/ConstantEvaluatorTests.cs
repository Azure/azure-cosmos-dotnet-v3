//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ConstantEvaluatorTests
    {
        [TestMethod]
        public void ClosuresInsideMemberInitExpressionAreFolded()
        {
            int captured = 1;
            Expression<Func<int, object>> expression = x => new TestClass { Property = x + captured };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            MemberInitExpression memberInit = AssertMemberInit(folded, typeof(TestClass));
            MemberAssignment assignment = AssertSingleMemberAssignment(memberInit, nameof(TestClass.Property));
            BinaryExpression binary = AssertBinary(assignment.Expression, ExpressionType.Add);
            AssertParameter(binary.Left, "x");
            AssertConstant(binary.Right, 1);
        }

        [TestMethod]
        public void ClosuresInsideAnonymousObjectAreFolded()
        {
            int captured = 1;
            Expression<Func<int, object>> expression = x => new { Property = x + captured };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            NewExpression newExpression = AssertNew(folded);
            Assert.AreEqual(1, newExpression.Arguments.Count);
            BinaryExpression binary = AssertBinary(newExpression.Arguments[0], ExpressionType.Add);
            AssertParameter(binary.Left, "x");
            AssertConstant(binary.Right, 1);
        }

        // Regression test for https://github.com/Azure/azure-cosmos-dotnet-v3/issues/1664.
        // Mirrors the original bug report shape: a dictionary indexer keyed by a closure-captured
        // variable, used inside a class member initializer. Before the fix this folded to
        // `{"k": "Test"}["k"]` instead of `"Test"`, producing invalid SQL at the Cosmos backend.
        // The parameter reference `q.StringProperty` anchors the MemberInit so the Nominator
        // cannot collapse the entire expression to a single constant — only the closure-only
        // dictionary indexer sub-tree should fold.
        [TestMethod]
        public void ClosuresUsedAsDictionaryIndexerInsideMemberInitAreFolded()
        {
            Dictionary<string, string> map = new Dictionary<string, string> { ["k"] = "Test" };
            string capturedKey = "k";
            Expression<Func<TestClass, object>> expression =
                q => new TestClass { StringProperty = q.StringProperty + map[capturedKey] };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            MemberInitExpression memberInit = AssertMemberInit(folded, typeof(TestClass));
            MemberAssignment assignment = AssertSingleMemberAssignment(memberInit, nameof(TestClass.StringProperty));
            BinaryExpression binary = AssertBinary(assignment.Expression, ExpressionType.Add);
            // Left side stays as `q.StringProperty` (parameter-bound member access).
            MemberExpression leftMember = binary.Left as MemberExpression;
            Assert.IsNotNull(leftMember, $"Expected MemberExpression on left of Add but got {binary.Left?.NodeType.ToString() ?? "<null>"}.");
            AssertParameter(leftMember.Expression, "q");
            Assert.AreEqual(nameof(TestClass.StringProperty), leftMember.Member.Name);
            // Right side must be the folded literal — not a `Dictionary[indexer]` expression.
            AssertConstant(binary.Right, "Test");
        }

        [TestMethod]
        public void ClosuresInsideNestedMemberInitAreFolded()
        {
            int captured = 7;
            Expression<Func<int, object>> expression = x => new OuterTestClass
            {
                Inner = new TestClass { Property = x + captured }
            };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            MemberInitExpression outerInit = AssertMemberInit(folded, typeof(OuterTestClass));
            MemberAssignment innerAssignment = AssertSingleMemberAssignment(outerInit, nameof(OuterTestClass.Inner));
            MemberInitExpression innerInit = AssertMemberInit(innerAssignment.Expression, typeof(TestClass));
            MemberAssignment propertyAssignment = AssertSingleMemberAssignment(innerInit, nameof(TestClass.Property));
            BinaryExpression binary = AssertBinary(propertyAssignment.Expression, ExpressionType.Add);
            AssertParameter(binary.Left, "x");
            AssertConstant(binary.Right, 7);
        }

        [TestMethod]
        public void ClosuresInsideMemberMemberBindingAreFolded()
        {
            int captured = 9;
            Expression<Func<int, object>> expression = x => new OuterTestClass
            {
                Inner = { Property = x + captured }
            };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            MemberInitExpression outerInit = AssertMemberInit(folded, typeof(OuterTestClass));
            Assert.AreEqual(1, outerInit.Bindings.Count);
            MemberMemberBinding nested = outerInit.Bindings[0] as MemberMemberBinding;
            Assert.IsNotNull(nested, "Expected a MemberMemberBinding for nested initializer syntax.");
            Assert.AreEqual(nameof(OuterTestClass.Inner), nested.Member.Name);
            Assert.AreEqual(1, nested.Bindings.Count);
            MemberAssignment propertyAssignment = nested.Bindings[0] as MemberAssignment;
            Assert.IsNotNull(propertyAssignment, "Expected MemberAssignment inside MemberMemberBinding.");
            Assert.AreEqual(nameof(TestClass.Property), propertyAssignment.Member.Name);
            BinaryExpression binary = AssertBinary(propertyAssignment.Expression, ExpressionType.Add);
            AssertParameter(binary.Left, "x");
            AssertConstant(binary.Right, 9);
        }

        [TestMethod]
        public void ClosuresInsideMemberListBindingAreFolded()
        {
            int captured = 11;
            Expression<Func<int, object>> expression = x => new OuterWithListTestClass
            {
                Items = { x + captured }
            };

            Expression folded = ConstantEvaluator.PartialEval(expression.Body);

            MemberInitExpression outerInit = AssertMemberInit(folded, typeof(OuterWithListTestClass));
            Assert.AreEqual(1, outerInit.Bindings.Count);
            MemberListBinding listBinding = outerInit.Bindings[0] as MemberListBinding;
            Assert.IsNotNull(listBinding, "Expected a MemberListBinding for collection initializer syntax.");
            Assert.AreEqual(nameof(OuterWithListTestClass.Items), listBinding.Member.Name);
            Assert.AreEqual(1, listBinding.Initializers.Count);
            Assert.AreEqual(1, listBinding.Initializers[0].Arguments.Count);
            BinaryExpression binary = AssertBinary(listBinding.Initializers[0].Arguments[0], ExpressionType.Add);
            AssertParameter(binary.Left, "x");
            AssertConstant(binary.Right, 11);
        }

        private static MemberInitExpression AssertMemberInit(Expression expression, Type expectedType)
        {
            MemberInitExpression memberInit = expression as MemberInitExpression;
            Assert.IsNotNull(memberInit, $"Expected MemberInitExpression but got {expression?.NodeType.ToString() ?? "<null>"}.");
            Assert.AreEqual(expectedType, memberInit.Type);
            return memberInit;
        }

        private static NewExpression AssertNew(Expression expression)
        {
            NewExpression newExpression = expression as NewExpression;
            Assert.IsNotNull(newExpression, $"Expected NewExpression but got {expression?.NodeType.ToString() ?? "<null>"}.");
            return newExpression;
        }

        private static MemberAssignment AssertSingleMemberAssignment(MemberInitExpression memberInit, string memberName)
        {
            Assert.AreEqual(1, memberInit.Bindings.Count, $"Expected a single binding for member '{memberName}'.");
            MemberAssignment assignment = memberInit.Bindings[0] as MemberAssignment;
            Assert.IsNotNull(assignment, $"Expected MemberAssignment for member '{memberName}' but got {memberInit.Bindings[0].BindingType}.");
            Assert.AreEqual(memberName, assignment.Member.Name);
            return assignment;
        }

        private static BinaryExpression AssertBinary(Expression expression, ExpressionType nodeType)
        {
            BinaryExpression binary = expression as BinaryExpression;
            Assert.IsNotNull(binary, $"Expected BinaryExpression but got {expression?.NodeType.ToString() ?? "<null>"}.");
            Assert.AreEqual(nodeType, binary.NodeType);
            return binary;
        }

        private static void AssertParameter(Expression expression, string parameterName)
        {
            ParameterExpression parameter = expression as ParameterExpression;
            Assert.IsNotNull(parameter, $"Expected ParameterExpression '{parameterName}' but got {expression?.NodeType.ToString() ?? "<null>"}.");
            Assert.AreEqual(parameterName, parameter.Name);
        }

        private static void AssertConstant<T>(Expression expression, T expectedValue)
        {
            ConstantExpression constant = expression as ConstantExpression;
            Assert.IsNotNull(constant, $"Expected ConstantExpression with value '{expectedValue}' but got {expression?.NodeType.ToString() ?? "<null>"}.");
            Assert.AreEqual(expectedValue, constant.Value);
        }

        private class TestClass
        {
            public int Property { get; set; }

            public string StringProperty { get; set; }
        }

        private class OuterTestClass
        {
            public TestClass Inner { get; set; } = new TestClass();
        }

        private class OuterWithListTestClass
        {
            public List<int> Items { get; } = new List<int>();
        }
    }
}
