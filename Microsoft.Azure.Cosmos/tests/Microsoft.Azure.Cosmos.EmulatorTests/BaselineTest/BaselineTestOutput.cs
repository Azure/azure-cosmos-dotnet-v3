//-----------------------------------------------------------------------
// <copyright file="BaselineTestOutput.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    /// <summary>
    /// Abstract base class for BaselineTestOutput.
    /// </summary>
    public abstract class BaselineTestOutput
    {
        /// <summary>
        /// Serializes the output as xml using the provided writer.
        /// The derived class will have to override this, since every output is serialized a little differently.
        /// </summary>
        /// <param name="xmlWriter">The writer to use.</param>
        public abstract void SerializeAsXml(XmlWriter xmlWriter);
    }

    /// <summary>
    /// Composes multiple BaselineTestOutput into a single output
    /// </summary>
    public sealed class AggregateBaselineTestOutput : BaselineTestOutput
    {
        private readonly IEnumerable<BaselineTestOutput> baselineTestOutputs;

        public AggregateBaselineTestOutput(IEnumerable<BaselineTestOutput> baselineTestOutputs)
        {
            if (baselineTestOutputs == null)
            {
                throw new ArgumentNullException($"{nameof(baselineTestOutputs)} must not be null.");
            }

            if (baselineTestOutputs.Any(x => x == null))
            {
                throw new ArgumentException($"{nameof(baselineTestOutputs)} must not have null elements.");
            }

            this.baselineTestOutputs = baselineTestOutputs.ToList();
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            foreach (BaselineTestOutput output in this.baselineTestOutputs)
            {
                xmlWriter.WriteStartElement("SubOutput");
                output.SerializeAsXml(xmlWriter);
                xmlWriter.WriteEndElement();
            }
        }
    }
}
