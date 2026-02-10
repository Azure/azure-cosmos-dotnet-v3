//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if !NET7_0_OR_GREATER // Class is defined in NET7 and higher so don't define it there.

#pragma warning disable NamespaceMatchesFolderStructure // The namespace needs to be System.Diagnostics.CodeAnalysis.
namespace System.Diagnostics.CodeAnalysis
#pragma warning restore NamespaceMatchesFolderStructure
{
    /// <summary>
    /// Partial copy of NET7's StringSyntaxAttribute.
    /// </summary>
    /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/StringSyntaxAttribute.cs
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
#if DOCDBCLIENT // We don't want to update the public API in the client
    internal
#else
    public
#endif
    sealed class StringSyntaxAttribute : Attribute
    {
        /// <summary>The syntax identifier for strings containing composite formats for string formatting.</summary>
        public const string CompositeFormat = nameof(CompositeFormat);

        /// <summary>The syntax identifier for strings containing date format specifiers.</summary>
        public const string DateOnlyFormat = nameof(DateOnlyFormat);

        /// <summary>The syntax identifier for strings containing date and time format specifiers.</summary>
        public const string DateTimeFormat = nameof(DateTimeFormat);

        /// <summary>The syntax identifier for strings containing <see cref="Enum"/> format specifiers.</summary>
        public const string EnumFormat = nameof(EnumFormat);

        /// <summary>The syntax identifier for strings containing <see cref="Guid"/> format specifiers.</summary>
        public const string GuidFormat = nameof(GuidFormat);

        /// <summary>The syntax identifier for strings containing JavaScript Object Notation (JSON).</summary>
        public const string Json = nameof(Json);

        /// <summary>The syntax identifier for strings containing numeric format specifiers.</summary>
        public const string NumericFormat = nameof(NumericFormat);

        /// <summary>The syntax identifier for strings containing regular expressions.</summary>
        public const string Regex = nameof(Regex);

        /// <summary>The syntax identifier for strings containing time format specifiers.</summary>
        public const string TimeOnlyFormat = nameof(TimeOnlyFormat);

        /// <summary>The syntax identifier for strings containing <see cref="TimeSpan"/> format specifiers.</summary>
        public const string TimeSpanFormat = nameof(TimeSpanFormat);

        /// <summary>The syntax identifier for strings containing URIs.</summary>
        public const string Uri = nameof(Uri);

        /// <summary>The syntax identifier for strings containing XML.</summary>
        public const string Xml = nameof(Xml);

        [SuppressMessage("Performance", "CA1825:Avoid zero-length array allocations", Justification = "Compatibility with lower versions of .NET")]
        private static readonly object[] DefaultArguments = new object[0];

        /// <summary>
        /// Initializes a new instance of the <see cref="StringSyntaxAttribute"/> class.
        /// </summary>
        public StringSyntaxAttribute(string syntax)
            : this(syntax, DefaultArguments)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringSyntaxAttribute"/> class.
        /// </summary>
        public StringSyntaxAttribute(string syntax, params object[] arguments)
        {
            this.Syntax = syntax;
            this.Arguments = arguments;
        }

        /// <summary>Gets the identifier of the syntax used.</summary>
        public string Syntax { get; }

        /// <summary>Optional arguments associated with the specific syntax employed.</summary>
        public object[] Arguments { get; }
    }
}

#endif
