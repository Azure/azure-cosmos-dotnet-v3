//-----------------------------------------------------------------------
// <copyright file="IJsonNavigator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a writer that provides a fast, non-cached, forward-only way of generating
    /// </summary>
    internal class PartitionKeyIntercepterJsonTextWriter : JsonTextWriter
    {
        private readonly ConcurrentQueue<string> partitionKeyPathTokens;

        /// <summary>
        /// Initializes a new instance of the Microsoft.Azure.Cosmos.Json class using
        ///  the specified System.IO.TextWriter.
        /// </summary>
        /// <param name="textWriter">The System.IO.TextWriter to write to.</param>
        /// <param name="partitionKeyPathTokens">The partition key path tokens.</param>
        public PartitionKeyIntercepterJsonTextWriter(TextWriter textWriter, 
            IList<string> partitionKeyPathTokens) : base(textWriter)
        {
            if(partitionKeyPathTokens != null)
            {
                this.partitionKeyPathTokens = new ConcurrentQueue<string>(partitionKeyPathTokens);
            }            
        }

        public object PartitionKey { get; private set; }
        public bool HasPartitionKey { get; private set; }

        public override void WritePropertyName(string name, 
            bool escape)
        {
            IsPartOfPartitionKeyPath(name, escape);
            base.WritePropertyName(name, escape);            
        }

        public override void WritePropertyName(string name)
        {
            IsPartOfPartitionKeyPath(name);
            base.WritePropertyName(name);
        }

        public override Task WritePropertyNameAsync(string name, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IsPartOfPartitionKeyPath(name);
            return base.WritePropertyNameAsync(name, cancellationToken);
        }

        public override Task WritePropertyNameAsync(string name, 
            bool escape, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IsPartOfPartitionKeyPath(name, escape);
            return base.WritePropertyNameAsync(name, escape, cancellationToken);
        }

        public override void WriteUndefined()
        {
            SetPartitionKey(Documents.Undefined.Value);
            base.WriteUndefined();
        }
        
        public override Task WriteUndefinedAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(Documents.Undefined.Value);
            return base.WriteUndefinedAsync(cancellationToken);
        }
        
        public override void WriteValue(DateTime value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(Uri value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(object value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(TimeSpan value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(Guid value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(string value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(int value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }
        
        public override void WriteValue(uint value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(byte[] value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(long value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(float value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(float? value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(DateTimeOffset value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(double? value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(bool value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(short value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(ushort value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }
        
        public override void WriteValue(char value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(byte value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(sbyte value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(decimal value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(ulong value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override void WriteValue(double value)
        {
            SetPartitionKey(value);
            base.WriteValue(value);
        }

        public override Task WriteValueAsync(char value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }

        public override Task WriteValueAsync(char? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }

        public override Task WriteValueAsync(bool? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }

        public override Task WriteValueAsync(sbyte value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }

        public override Task WriteValueAsync(object value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(long? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }

        public override Task WriteValueAsync(long value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(int? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(int value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(Guid? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(Guid value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(bool value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(float? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(double? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(double value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(decimal? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(decimal value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(DateTimeOffset? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(DateTimeOffset value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(DateTime? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(DateTime value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(float value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(short value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(sbyte? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(string value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(byte value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(byte? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
       
        public override Task WriteValueAsync(ushort? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(ushort value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(short? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(byte[] value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(Uri value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(ulong? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(ulong value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
       
        public override Task WriteValueAsync(uint? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(uint value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(TimeSpan? value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
        
        public override Task WriteValueAsync(TimeSpan value, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SetPartitionKey(value);
            return base.WriteValueAsync(value, cancellationToken);
        }
           
        private void SetPartitionKey(object value)
        {
            if(partitionKeyPathTokens == null)
            {
                return;
            }

            if(partitionKeyPathTokens.Count < 1 && !this.HasPartitionKey)
            {
                this.PartitionKey = value;
                HasPartitionKey = true;
                return;
            }

            return;
        }

        private void IsPartOfPartitionKeyPath(string value, 
            bool escape = false)
        {
            if(partitionKeyPathTokens == null)
            {
                return;
            }

            if(partitionKeyPathTokens.Count < 1)
            {
                return;
            }

            if (escape == false && partitionKeyPathTokens.TryPeek(out string pk))
            {
                if (pk.Equals(value))
                {
                    partitionKeyPathTokens.TryDequeue(out string result);
                    return;
                }
            }

            return;
        }
    }
}
