//-----------------------------------------------------------------------
// <copyright file="BaselineTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Test.BaselineTest
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Xml;
    using VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Base class for all baseline tests.
    /// </summary>
    /// <typeparam name="TInput">The type of the input for each test (which derives from BaselineTestInput).</typeparam>
    /// <typeparam name="TOutput">The type of the output for each test (which derives from BaselineTestOutput).</typeparam>
    [TestCategory("UpdateContract")]
    public abstract class BaselineTests<TInput, TOutput> where TInput : BaselineTestInput where TOutput : BaselineTestOutput
    {
        /// <summary>
        /// Directory where all the baselines will exist.
        /// </summary>
        private const string TestBaslineDir = "BaselineTest\\TestBaseline";

        /// <summary>
        /// Directory where all the temporary baseline outputs will be placed.
        /// </summary>
        private const string TestOutputDir = "BaselineTest\\TestOutput";

        /// <summary>
        /// The file extension for xml.
        /// </summary>
        private const string XmlFileExtension = "xml";

        /// <summary>
        /// Executes a whole suite of baselines, which corresponds to a visual studio test method.
        /// </summary>
        /// <param name="inputs">The inputs that you want executed.</param>
        /// <param name="testSuiteName">The name of the test suite which will just be the method name by default.</param>
        public void ExecuteTestSuite(IEnumerable<TInput> inputs, [CallerMemberName] string testSuiteName = "")
        {
            // Preconditions.
            if (inputs == null || inputs.Count() == 0)
            {
                throw new ArgumentException($"{nameof(inputs)} must not be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(testSuiteName))
            {
                throw new ArgumentException($"{nameof(testSuiteName)} must not be null, empty, or whitespace.");
            }

            // Foreach input generate an output to form a result.
            List<BaselineTestResult> baselineTestResults = new List<BaselineTestResult>();
            int inputId = 0;
            int totalInputs = inputs.Count();
            Debug.WriteLine($"Total inputs: {totalInputs}");
            foreach (TInput input in inputs)
            {
                Debug.WriteLine($"Execute input {++inputId}: {input.Description}..");
                TOutput output = this.ExecuteTest(input);
                baselineTestResults.Add(new BaselineTestResult(input, output));
            }

            // Standard xml setttings for pretty printing.
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = true,
            };

            // The filename will be the classname.testsuitename.xml
            string filename = $"{this.GetType().Name}.{testSuiteName}.{XmlFileExtension}";

            // Create the output directory if it doesn't exist.
            Directory.CreateDirectory(TestOutputDir);
            string outputPath = Path.Combine(TestOutputDir, filename);
            string baselinePath = Path.Combine(TestBaslineDir, filename);

            // Write the xml out in the following format:
            // <Results>
            //  <Result>
            //      <Input>
            //      ..
            //      <Input/>
            //      <Output>
            //      ..
            //      <Output/>
            //  <Result/>
            // <Results/>
            using (XmlWriter writer = XmlWriter.Create(outputPath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Results");
                foreach (BaselineTestResult baselineTestResult in baselineTestResults)
                {
                    baselineTestResult.SerializeAsXml(writer);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            // Compare the output to the baseline and fail if they differ.
            string outputText = Regex.Replace(File.ReadAllText(outputPath), @"\s+", "");
            string baselineText = Regex.Replace(File.ReadAllText(baselinePath), @"\s+", "");
            int commonPrefixLength = 0;
            foreach (Tuple<char, char> characters in outputText.Zip(baselineText, (first, second) => new Tuple<char, char>(first, second)))
            {
                if (characters.Item1 == characters.Item2)
                {
                    commonPrefixLength++;
                }
                else
                {
                    break;
                }
            }

            string baselineTextSuffix = new string(baselineText.Skip(Math.Max(commonPrefixLength - 30, 0)).Take(100).ToArray());
            string outputTextSuffix = new string(outputText.Skip(Math.Max(commonPrefixLength - 30, 0)).Take(100).ToArray());

            bool matched = baselineText.Equals(outputText);
            if (!matched)
            {
                Debug.WriteLine("Expected: {0}, Actual: {1}", baselineText, outputText);
            }

            Assert.IsTrue(
                matched,
                $@" Baseline File {Path.GetFullPath(baselinePath)},
                    Output File {Path.GetFullPath(outputPath)},
                    Expected: {baselineTextSuffix},
                    Actual:   {outputTextSuffix}");
        }

        /// <summary>
        /// Executes the single Test for the test suite.
        /// It is the derived classes job to override this method, since only that class know what to do with each test input.
        /// </summary>
        /// <param name="input">The input type of the derived class.</param>
        /// <returns>An output after executing with the input.</returns>
        public abstract TOutput ExecuteTest(TInput input);

        /// <summary>
        /// Utility struct that just holds together an input and output together to make a result.
        /// </summary>
        private readonly struct BaselineTestResult
        {
            /// <summary>
            /// Initializes a new instance of the BaselineTestResult struct.
            /// </summary>
            /// <param name="input">The input.</param>
            /// <param name="output">The output.</param>
            public BaselineTestResult(BaselineTestInput input, BaselineTestOutput output)
            {
                this.Input = input ?? throw new ArgumentNullException($"{nameof(input)} must not be null.");
                this.Output = output ?? throw new ArgumentNullException($"{nameof(output)} must not be null.");
            }

            /// <summary>
            /// Gets the input for the baseline result.
            /// </summary>
            public BaselineTestInput Input { get; }

            /// <summary>
            /// Gets the output from executing with the input.
            /// </summary>
            public BaselineTestOutput Output { get; }

            /// <summary>
            /// Serializes the result to the provided xml writer.
            /// </summary>
            /// <param name="xmlWriter">The xml writer to write with.</param>
            public void SerializeAsXml(XmlWriter xmlWriter)
            {
                if (xmlWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(xmlWriter)} must not be null.");
                }

                xmlWriter.WriteStartElement("Result");
                xmlWriter.WriteStartElement("Input");
                this.Input.SerializeAsXml(xmlWriter);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Output");
                this.Output.SerializeAsXml(xmlWriter);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
            }
        }
    }
}