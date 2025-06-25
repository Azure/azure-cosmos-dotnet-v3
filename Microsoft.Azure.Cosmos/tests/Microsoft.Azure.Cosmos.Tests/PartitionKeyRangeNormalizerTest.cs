namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap;
    using Microsoft.Azure.Documents.Routing;

    [TestClass]
    public class PartitionKeyRangeNormalizerTest
    {
        [DataTestMethod]
        [DataRow("", "03559A67F2724111B5E565DFA8711AF2", "", "03559A67F2724111B5E565DFA8711AF200000000000000000000000000000000", 2)]
        [DataRow("", "FF", "", "FF", 2)]
        [DataRow("03559A67F2724111B5E565DFA8711AF2", "0BD3FBE846AF75790CE63F78B1A8162B", "03559A67F2724111B5E565DFA8711AF200000000000000000000000000000000", "0BD3FBE846AF75790CE63F78B1A8162B00000000000000000000000000000000", 2)]
        [DataRow("03559A67F2724111B5E565DFA8711AF2", "0BD3FBE846AF75790CE63F78B1A8162BFF", "03559A67F2724111B5E565DFA8711AF200000000000000000000000000000000", "0BD3FBE846AF75790CE63F78B1A8162BFF000000000000000000000000000000", 2)]
        [DataRow("03559A67F2724111B5E565DFA8711AF2", "0BD3FBE846AF75790CE63F78B1A8162B00000000000000000000000000000000", "03559A67F2724111B5E565DFA8711AF200000000000000000000000000000000", "0BD3FBE846AF75790CE63F78B1A8162B00000000000000000000000000000000", 2)]
        [DataRow("", "03559A67F2724111B5E565DFA8711AF2", "", "03559A67F2724111B5E565DFA8711AF20000000000000000000000000000000000000000000000000000000000000000", 3)]
        [DataRow("03559A67F2724111B5E565DFA8711AF2", "0BD3FBE846AF75790CE63F78B1A8162B1BD3FBE846AF75790CE63F78B1A8162BFF", "03559A67F2724111B5E565DFA8711AF20000000000000000000000000000000000000000000000000000000000000000", "0BD3FBE846AF75790CE63F78B1A8162B1BD3FBE846AF75790CE63F78B1A8162BFF000000000000000000000000000000", 3)]
        [DataRow("03559A67F2724111B5E565DFA8711AF2", "03559A67F2724111B5E565DFA8711AF2FF", "03559A67F2724111B5E565DFA8711AF20000000000000000000000000000000000000000000000000000000000000000", "03559A67F2724111B5E565DFA8711AF2FF00000000000000000000000000000000000000000000000000000000000000", 3)]
        // Add more DataRow attributes here for additional test cases as needed
        public void TestRightNormalizedValueWhenNotFullySpecified_Param(
            string minValue,
            string maxValue,
            string expectedMinValue,
            string expectedMaxValue,
            int hpkLevels)
        {
            PartitionKeyDefinition partitionKeyDefinition = this.GeneratePartitionKeyDefinition(hpkLevels);

            Range<string> inputPkRange = new Range<string>(
                        minValue,
                        maxValue,
                        true,
                        false);
            IReadOnlyList<Range<string>> providedPartitionKeyRanges = new List<Range<string>> { inputPkRange };

            IReadOnlyList<Range<string>> outputPkRange = PartitionKeyRangeNormalizer.NormalizeRanges(providedPartitionKeyRanges, partitionKeyDefinition);

            Assert.AreEqual(1, outputPkRange.Count);

            Assert.IsTrue(outputPkRange[0].IsMinInclusive);
            Assert.IsFalse(outputPkRange[0].IsMaxInclusive);
            Assert.AreEqual(expectedMinValue, outputPkRange[0].Min);
            Assert.AreEqual(expectedMaxValue, outputPkRange[0].Max);
        }
        private PartitionKeyDefinition GeneratePartitionKeyDefinition(int levels)
        {
            System.Collections.ObjectModel.Collection<string> paths = new System.Collections.ObjectModel.Collection<string>();
            for (int i = 0; i < levels; i++)
            {
                paths.Add($"/{"id" + i}");
            }

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Paths = paths,
                Kind = PartitionKind.MultiHash
            };
            return partitionKeyDefinition;
        }
        
    }
}
