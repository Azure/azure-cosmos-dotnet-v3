namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ScalarExpressionTests : ParserTests
    {
        [TestMethod]
        [DataRow("[]", DisplayName = "Empty Array")]
        [DataRow("[1]", DisplayName = "Single Item Array")]
        [DataRow("[1, 2, 3]", DisplayName = "Multi Item Array")]
        public void ArrayCreateScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("[", DisplayName = "No closing brace")]
        [DataRow("]", DisplayName = "No opening brace")]
        [DataRow("[1,]", DisplayName = "trailing delimiter")]
        [DataRow("[,]", DisplayName = "delimiter no items")]
        public void ArrayCreateScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("ARRAY(SELECT *)", DisplayName = "Basic")]
        public void ArrayScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("ARRAY(SELECT *", DisplayName = "No closing parens")]
        [DataRow("ARRAY(123)", DisplayName = "not a subquery")]
        //[DataRow("ARAY(123)", DisplayName = "Wrong token")] -> becomes function call
        public void ArrayScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 BETWEEN 15 AND 1337", DisplayName = "Regular Betweeen")]
        [DataRow("42 NOT BETWEEN 15 AND 1337", DisplayName = "NOT Betweeen")]
        public void BetweenScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 15 AND 1337", DisplayName = "Missing Betweeen")]
        [DataRow("42 NOT 15 AND 1337", DisplayName = "NOT + Missing between")]
        [DataRow("42 BETWEEN 15 AND ", DisplayName = "Missing end range")]
        [DataRow("42 BETWEEN AND 1337", DisplayName = "missing start range")]
        [DataRow("BETWEEN 15 AND 1337", DisplayName = "missing needle")]
        public void BetweenScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 + 1337", DisplayName = "Plus")]
        [DataRow("42 AND 1337", DisplayName = "AND")]
        [DataRow("42 & 1337", DisplayName = "Bitwise AND")]
        [DataRow("42 | 1337", DisplayName = "Bitwise OR")]
        [DataRow("42 ^ 1337", DisplayName = "Bitwise XOR")]
        [DataRow("42 / 1337", DisplayName = "Division")]
        [DataRow("42 = 1337", DisplayName = "Equals")]
        [DataRow("42 > 1337", DisplayName = "Greater Than")]
        [DataRow("42 >= 1337", DisplayName = "Greater Than Or Equal To")]
        [DataRow("42 < 1337", DisplayName = "Less Than")]
        [DataRow("42 <= 1337", DisplayName = "Less Than Or Equal To")]
        [DataRow("42 % 1337", DisplayName = "Mod")]
        [DataRow("42 * 1337", DisplayName = "Multiplication")]
        [DataRow("42 != 1337", DisplayName = "Not Equals")]
        [DataRow("42 OR 1337", DisplayName = "OR")]
        [DataRow("42 || 1337", DisplayName = "String Concat")]
        [DataRow("42 - 1337", DisplayName = "Minus")]
        public void BinaryScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 +", DisplayName = "Missing Right")]
        [DataRow("AND 1337", DisplayName = "Missing Left")]
        [DataRow("42 # 1337", DisplayName = "Unknown Operator")]
        public void BinaryScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 ?? 1337")]
        public void CoalesceScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 ??", DisplayName = "Missing Right")]
        [DataRow("?? 1337", DisplayName = "Missing Left")]
        public void CoalesceScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 ? 123 : 1337", DisplayName = "Basic")]
        public void ConditionalScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow(" ? 123 : 1337", DisplayName = "Missing Condition")]
        [DataRow("42 ?  : 1337", DisplayName = "Missing if true")]
        [DataRow("42 ? 123 : ", DisplayName = "Missing if false")]
        public void ConditionalScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("EXISTS(SELECT *)", DisplayName = "Basic")]
        [DataRow("ExIsTS(SELECT *)", DisplayName = "case insensitive")]
        public void ExistsScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("EXISTS(SELECT *", DisplayName = "No closing parens")]
        [DataRow("EXISTS(123)", DisplayName = "not a subquery")]
        //[DataRow("EXITS(123)", DisplayName = "Wrong token")] -> becomes function call
        public void ExistsScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("ABS(-123)", DisplayName = "Basic")]
        [DataRow("PI()", DisplayName = "No Arguments")]
        [DataRow("STARTSWITH('asdf', 'as')", DisplayName = "multiple arguments")]
        [DataRow("udf.my_udf(-123)", DisplayName = "udf")]
        public void FunctionCallScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("ABS(-123", DisplayName = "missing brace")]
        [DataRow("*@#$('asdf')", DisplayName = "invalid identifier")]
        [DataRow("ABS('asdf',)", DisplayName = "trailing delimiter")]
        [DataRow("ABS(,)", DisplayName = "delimiter but no arguments")]
        public void FunctionCallScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 IN(42)", DisplayName = "Basic")]
        [DataRow("42 IN ('asdf', 'as')", DisplayName = "multiple arguments")]
        [DataRow("42 NOT IN (42)", DisplayName = "NOT IN")]
        public void InScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42 IN()", DisplayName = "Empty List")]
        [DataRow("42 IN (-123", DisplayName = "missing brace")]
        [DataRow("42 IN ('asdf',)", DisplayName = "trailing delimiter")]
        [DataRow("42 IN (,)", DisplayName = "delimiter but no arguments")]
        [DataRow("IN (-123", DisplayName = "missing needle")]
        [DataRow("42 IN ", DisplayName = "missing haystack")]
        [DataRow("42 inn (123)", DisplayName = "mispelled keyword")]
        public void InScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("42", DisplayName = "Integer")]
        [DataRow("1337.42", DisplayName = "Double")]
        [DataRow("-1337.42", DisplayName = "Negative Number")]
        //[DataRow("1E2", DisplayName = "E notation")] E notation doesn't round trip with AST.
        [DataRow("\"hello\"", DisplayName = "string with double quotes")]
        [DataRow("'hello'", DisplayName = "string with single quotes")]
        [DataRow("true", DisplayName = "true")]
        [DataRow("false", DisplayName = "false")]
        [DataRow("null", DisplayName = "null")]
        [DataRow("undefined", DisplayName = "undefined")]
        public void LiteralScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("-0.E", DisplayName = "Invalid Number")]
        [DataRow("\"hello", DisplayName = "string missing quote")]
        public void LiteralScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("c.arr[2]", DisplayName = "Basic")]
        [DataRow("c.arr[2 + 2]", DisplayName = "Basic")]
        public void MemberIndexerScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("c.array[]", DisplayName = "missing indexer expression")]
        public void MemberIndexerScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("{}", DisplayName = "Empty Object")]
        [DataRow("{ 'prop' : 42 }", DisplayName = "Single Property")]
        [DataRow("{ 'prop1' : 42, 'prop2' : 1337 }", DisplayName = "MultipleProperty")]
        public void ObjectCreateScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("{", DisplayName = "Missing Close Brace")]
        [DataRow("{ 'prop' : 42, }", DisplayName = "trailing delimiter")]
        [DataRow("{ , }", DisplayName = "delimiter but no properties")]
        [DataRow("{ 23 : 42, }", DisplayName = "non string property name")]
        public void ObjectCreateScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("c", DisplayName = "root")]
        [DataRow("c.arr", DisplayName = "Basic")]
        [DataRow("c.arr[2]", DisplayName = "ArrayIndex")]
        [DataRow("c.arr['asdf']", DisplayName = "StringIndex")]
        public void PropertyRefScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("c.2", DisplayName = "number with dot notation")]
        public void PropertyRefScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("(SELECT *)", DisplayName = "basic")]
        public void SubqueryScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("(SELECT *", DisplayName = "missing right parens")]
        [DataRow("SELECT *)", DisplayName = "missing left parens")]
        [DataRow("SELECT *", DisplayName = "missing both parens")]
        public void SubqueryScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("-42", DisplayName = "negative")]
        [DataRow("+42", DisplayName = "positive")]
        [DataRow("~42", DisplayName = "bitwise not")]
        [DataRow("NOT 42", DisplayName = "not")]
        public void UnaryScalarExpressionPositive(string scalarExpression)
        {
            ScalarExpressionTests.ValidateScalarExpression(scalarExpression);
        }

        [TestMethod]
        [DataRow("$42", DisplayName = "unknown operator")]
        public void UnaryScalarExpressionNegative(string scalarExpression)
        {
            ScalarExpressionTests.InvalidateScalarExpression(scalarExpression);
        }

        private static void ValidateScalarExpression(string scalarExpression)
        {
            Assert.IsNotNull(scalarExpression);
            string query = $"SELECT VALUE {scalarExpression}";
            ScalarExpressionTests.Validate(query);
        }

        private static void InvalidateScalarExpression(string scalarExpression)
        {
            Assert.IsNotNull(scalarExpression);
            string query = $"SELECT VALUE {scalarExpression}";
            ScalarExpressionTests.Invalidate(query);
        }
    }
}
