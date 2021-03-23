using System;
using System.Linq;
using FluentAssertions;
using HdrHistogram.Iteration;
using Xunit;

namespace HdrHistogram.UnitTests
{
    public static class HistogramAssert
    {
        public static void AreEqual(HistogramBase expected, HistogramBase actual)
        {
            Assert.Equal(expected.GetType(), actual.GetType());
            AreValueEqual(expected, actual);
        }

        public static void AreValueEqual(HistogramBase expected, HistogramBase actual)
        {
            expected.TotalCount.Should().Be(actual.TotalCount, "TotalCount property is not equal.");
            expected.Tag.Should().Be(actual.Tag, "Tag property is not equal.");
            expected.StartTimeStamp.Should().Be(actual.StartTimeStamp, "StartTimeStamp property is not equal.");
            expected.EndTimeStamp.Should().Be(actual.EndTimeStamp, "EndTimeStamp property is not equal.");
            expected.LowestTrackableValue.Should().Be(actual.LowestTrackableValue, "LowestTrackableValue property is not equal.");
            expected.HighestTrackableValue.Should().Be(actual.HighestTrackableValue, "HighestTrackableValue property is not equal.");
            expected.NumberOfSignificantValueDigits.Should().Be(actual.NumberOfSignificantValueDigits, "NumberOfSignificantValueDigits property is not equal.");

            var expectedValues = expected.AllValues().ToArray();
            var actualValues = actual.AllValues().ToArray();
            CollectionEquals(expectedValues, actualValues);
        }

        private static void CollectionEquals(HistogramIterationValue[] expected, HistogramIterationValue[] actual)
        {
            if (expected == null && actual == null)
                return;
            if(expected == null)
                throw new Exception("Expected null array");
            if (actual == null)
                throw new Exception("Unexpected null array");

            if(expected.Length != actual.Length)
                throw new Exception($"Expected length of {expected.Length}, but recieved {actual.Length}");

            for (int i = 0; i < expected.Length; i++)
            {
                var e = expected[i];
                var a = actual[i];
                if (HistogramIterationValueComparer.Instance.Compare(e, a) != 0)
                {
                    throw new Exception($"Values differ at index {i}. Expected {e}, but recieved {a}");
                }
            }
        }
    }
}