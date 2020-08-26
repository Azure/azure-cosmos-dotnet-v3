//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test.Management.Tests.LinqProviderTests
{
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Services.Management.Tests.BaselineTest;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Xml;
    using static LinqConstantFoldingBaselineTests;

    [TestClass]
    public class LinqConstantFoldingBaselineTests : BaselineTests<LinqExpressionInput, LinqExpressionOutput>
    {
        [TestMethod]
        [Owner("khdang")]
        public void TestUnaryOperators()
        {
            List<LinqExpressionInput> inputs = new List<LinqExpressionInput>();
            // Unary plus (+)
            inputs.Add(new LinqExpressionInput("Unary plus (+)", Expression.MakeUnary(ExpressionType.UnaryPlus, Expression.Constant(15), typeof(int))));
            // numeric negation (-)
            inputs.Add(new LinqExpressionInput("Numeric negation (-)", Expression.MakeUnary(ExpressionType.Negate, Expression.Constant(-15), typeof(int))));
            // logical negation (!)
            inputs.Add(new LinqExpressionInput("Logical negation (!) false", Expression.MakeUnary(ExpressionType.Not, Expression.Constant(true), typeof(bool))));
            inputs.Add(new LinqExpressionInput("Logical negation (!) true", Expression.MakeUnary(ExpressionType.Not, Expression.Constant(false), typeof(bool))));
            // bitwise complement 
            inputs.Add(new LinqExpressionInput("Bitwise compliment", Expression.MakeUnary(ExpressionType.OnesComplement, Expression.Constant(0), typeof(int))));
            inputs.Add(new LinqExpressionInput("Bitwise compliment #2", Expression.MakeUnary(ExpressionType.OnesComplement, Expression.Constant(0x111), typeof(int))));
            inputs.Add(new LinqExpressionInput("Bitwise compliment #3", Expression.MakeUnary(ExpressionType.OnesComplement, Expression.Constant(0xfffff), typeof(int))));
            inputs.Add(new LinqExpressionInput("Bitwise compliment #4", Expression.MakeUnary(ExpressionType.OnesComplement, Expression.Constant(0x8888), typeof(int))));
            inputs.Add(new LinqExpressionInput("Bitwise compliment #5", Expression.MakeUnary(ExpressionType.OnesComplement, Expression.Constant(0x22000022), typeof(int))));
            // increment
            inputs.Add(new LinqExpressionInput("Increment", Expression.MakeUnary(ExpressionType.Increment, Expression.Constant(1.5), typeof(double))));
            // decrement
            inputs.Add(new LinqExpressionInput("Decrement", Expression.MakeUnary(ExpressionType.Decrement, Expression.Constant(1.5), typeof(double))));
            // negate
            inputs.Add(new LinqExpressionInput("Negate", Expression.Negate(Expression.Constant(2))));
            // not
            inputs.Add(new LinqExpressionInput("Not", Expression.Not(Expression.Constant(2))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestBinaryOperators()
        {
            var inputs = new List<LinqExpressionInput>();
            inputs.Add(new LinqExpressionInput("true true equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(true), Expression.Constant(true))));
            inputs.Add(new LinqExpressionInput("true true not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(true), Expression.Constant(true))));
            inputs.Add(new LinqExpressionInput("false true equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(false), Expression.Constant(true))));
            inputs.Add(new LinqExpressionInput("false true not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(false), Expression.Constant(true))));
            inputs.Add(new LinqExpressionInput("false false equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(false), Expression.Constant(false))));
            inputs.Add(new LinqExpressionInput("false false not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(false), Expression.Constant(false))));
            inputs.Add(new LinqExpressionInput("null null equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(null), Expression.Constant(null))));
            inputs.Add(new LinqExpressionInput("null null not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(null), Expression.Constant(null))));
            inputs.Add(new LinqExpressionInput("1 1 equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 1 not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 1 <", Expression.MakeBinary(ExpressionType.LessThan, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 1 <=", Expression.MakeBinary(ExpressionType.LessThanOrEqual, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 1 >", Expression.MakeBinary(ExpressionType.GreaterThan, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 1 >=", Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, Expression.Constant(1), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 <", Expression.MakeBinary(ExpressionType.LessThan, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 <=", Expression.MakeBinary(ExpressionType.LessThanOrEqual, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 >", Expression.MakeBinary(ExpressionType.GreaterThan, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("2 1 >=", Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, Expression.Constant(2), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("1 2 equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("1 2 not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("1 2 <", Expression.MakeBinary(ExpressionType.LessThan, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("1 2 <=", Expression.MakeBinary(ExpressionType.LessThanOrEqual, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("1 2 >", Expression.MakeBinary(ExpressionType.GreaterThan, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("1 2 >=", Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("a a equal", Expression.MakeBinary(ExpressionType.Equal, Expression.Constant("a"), Expression.Constant("a"))));
            inputs.Add(new LinqExpressionInput("a a not equal", Expression.MakeBinary(ExpressionType.NotEqual, Expression.Constant("a"), Expression.Constant("a"))));

            inputs.Add(new LinqExpressionInput("Add", Expression.MakeBinary(ExpressionType.Add, Expression.Constant(12), Expression.Constant(34))));
            inputs.Add(new LinqExpressionInput("Subtract", Expression.MakeBinary(ExpressionType.Subtract, Expression.Constant(45), Expression.Constant(34))));
            inputs.Add(new LinqExpressionInput("Multiply", Expression.MakeBinary(ExpressionType.Multiply, Expression.Constant(10), Expression.Constant(20))));
            inputs.Add(new LinqExpressionInput("Divide", Expression.MakeBinary(ExpressionType.Divide, Expression.Constant(10), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("Modulo", Expression.MakeBinary(ExpressionType.Modulo, Expression.Constant(101), Expression.Constant(3))));
            inputs.Add(new LinqExpressionInput("And", Expression.MakeBinary(ExpressionType.And, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("Or", Expression.MakeBinary(ExpressionType.Or, Expression.Constant(3), Expression.Constant(4))));
            inputs.Add(new LinqExpressionInput("ExclusiveOr", Expression.MakeBinary(ExpressionType.ExclusiveOr, Expression.Constant(5), Expression.Constant(6))));
            inputs.Add(new LinqExpressionInput("LeftShift", Expression.MakeBinary(ExpressionType.LeftShift, Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("RightShift", Expression.MakeBinary(ExpressionType.RightShift, Expression.Constant(100), Expression.Constant(1))));

            int constInt = 2;
            var paramx = Expression.Parameter(typeof(int), "x");
            inputs.Add(new LinqExpressionInput("Add constInt", Expression.Add(Expression.Constant(constInt), Expression.Constant(6))));
            inputs.Add(new LinqExpressionInput("Add param", Expression.Add(paramx, Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("AndAlso", Expression.AndAlso(Expression.Constant(true), Expression.Constant(false))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Owner("khdang")]
        public void TestOtherExpressions()
        {
            List<LinqExpressionInput> inputs = new List<LinqExpressionInput>();
            // Parameter
            inputs.Add(new LinqExpressionInput("param", Expression.Parameter(typeof(int), "Param")));
            //    new TestExpression(paramx, true, "x"),
            // New array
            inputs.Add(new LinqExpressionInput("new array with 1 elem", Expression.NewArrayInit(typeof(int), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("new array with 2 elems", Expression.NewArrayInit(typeof(int), Expression.Constant(1), Expression.Constant(2))));
            inputs.Add(new LinqExpressionInput("new array with different types", Expression.NewArrayInit(typeof(object), Expression.Constant(1, typeof(object)), Expression.Constant("two", typeof(object)))));
            // Array index
            inputs.Add(new LinqExpressionInput("array index", Expression.ArrayIndex(Expression.NewArrayInit(typeof(int), Expression.Constant(3), Expression.Constant(4)), Expression.Constant(1))));
            inputs.Add(new LinqExpressionInput("int?", Expression.Constant(1, typeof(int?))));
            // Nullable types
            inputs.Add(new LinqExpressionInput("null", Expression.Constant(null)));
            inputs.Add(new LinqExpressionInput("int?", Expression.Constant(1, typeof(int?))));
            this.ExecuteTestSuite(inputs);
            // Constants
            inputs.Add(new LinqExpressionInput("null int", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("int? null", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("int?", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("char?", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("bool?", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("int", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("double", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("char", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("string", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("true", Expression.Constant(1, typeof(int?))));
            inputs.Add(new LinqExpressionInput("null", Expression.Constant(1, typeof(int?))));
        }

        public override LinqExpressionOutput ExecuteTest(LinqExpressionInput input)
        {
            try
            {
                string translation = SqlTranslator.TranslateExpression(input.expression);
                string translationOld = SqlTranslator.TranslateExpressionOld(input.expression);
                return new LinqExpressionOutput(translation, translationOld);
            }
            catch (Exception e)
            {
                return new LinqExpressionOutput(null, null, e.Message);
            }
        }

        public sealed class LinqExpressionInput : BaselineTestInput
        {
            internal Expression expression { get; }
            internal string errorMessage;

            internal LinqExpressionInput(string description, Expression expr, string errorMsg = null)
                : base(description)
            {
                this.expression = expr ?? throw new ArgumentNullException($"{nameof(expr)} must not be null.");
                this.errorMessage = errorMsg;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                if (xmlWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
                }

                xmlWriter.WriteStartElement("Description");
                xmlWriter.WriteCData(this.Description);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Expression");
                xmlWriter.WriteCData(this.expression.ToString());
                xmlWriter.WriteEndElement();
                if (this.errorMessage != null)
                {
                    xmlWriter.WriteStartElement("ErrorMessage");
                    xmlWriter.WriteCData(this.errorMessage);
                    xmlWriter.WriteEndElement();
                }
            }
        }

        public sealed class LinqExpressionOutput : BaselineTestOutput
        {
            internal string translationOutput { get; }
            internal string translationOldOutput { get; }
            internal string errorMessage { get; }

            internal LinqExpressionOutput(string translationOutput, string translationOldOutput = null, string errorMsg = null)
            {
                this.translationOutput = translationOutput;
                this.translationOldOutput = translationOldOutput;
                this.errorMessage = errorMsg;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                if (this.errorMessage == null)
                {
                    xmlWriter.WriteStartElement("TranslationOutput");
                    xmlWriter.WriteCData(this.translationOutput);
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteStartElement("TranslationOldOutput");
                    xmlWriter.WriteCData(this.translationOutput);
                    xmlWriter.WriteEndElement();
                }
                else
                {
                    xmlWriter.WriteStartElement("ErrorMessage");
                    xmlWriter.WriteCData(this.errorMessage);
                    xmlWriter.WriteEndElement();
                }
            }
        }
    }
}
