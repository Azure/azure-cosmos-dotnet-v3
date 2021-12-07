namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using BenchmarkDotNet.Attributes;

    public class LinqToSqlBenchmark
    {
        private class BenchmarkDocument
        {
            public string Property { get; set; }
        }

        [Benchmark(Baseline = true)]
        public void DelegatePropertyAccess()
        {
            var holder = new
            {
                Property = "test"
            };

            this.DoTranslate(doc => doc.Property == holder.Property);
        }

        [Benchmark]
        public void ReflectionPropertyAccess()
        {
            string variable = "test";

            this.DoTranslate(doc => doc.Property == variable);
        }

        private void DoTranslate<R>(Expression<Func<BenchmarkDocument, R>> expression)
        {
            SqlTranslator.TranslateExpression(expression.Body);
        }
    }
}
