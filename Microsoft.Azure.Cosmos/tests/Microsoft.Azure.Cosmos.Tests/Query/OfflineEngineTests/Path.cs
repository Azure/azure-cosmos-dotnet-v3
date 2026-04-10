//-----------------------------------------------------------------------
// <copyright file="Path.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Path class which will represent a path in a document tree.
    /// </summary>
    internal sealed class Path : IEquatable<Path>, IEnumerable<PathToken>, IComparable<Path>
    {
        /// <summary>
        /// List of string tokens stored start to end order that represents a path.
        /// </summary>
        private readonly List<PathToken> tokens;

        /// <summary>
        /// Delimiter used to join the tokens together.
        /// </summary>
        private readonly string delimiter;

        public Path()
            : this(delimiter: "/")
        {
        }

        public Path(string delimiter)
            : this(tokens: new List<PathToken>(), delimiter: delimiter)
        {
        }

        public Path(Path path)
            : this(tokens: new List<PathToken>(path.tokens), delimiter: path.delimiter)
        {
        }

        private Path(List<PathToken> tokens, string delimiter)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException($"{nameof(tokens)} can not be null");
            }

            foreach (PathToken token in tokens)
            {
                if (token == null)
                {
                    throw new ArgumentException($"{nameof(tokens)} can not have a null element");
                }
            }

            if (string.IsNullOrWhiteSpace(delimiter))
            {
                throw new ArgumentException($"{nameof(delimiter)} can not have a null, empty, or whitespace");
            }

            this.tokens = tokens;
            this.delimiter = delimiter;
        }

        public int Length => this.tokens.Count();

        public static Path CreateFromString(string stringPath, string delimiter = ".")
        {
            // This function does not handle array index path yet (ex. "/children/[3]/age").
            if (string.IsNullOrWhiteSpace(stringPath))
            {
                throw new ArgumentException($"{nameof(stringPath)} can not be null, empty, or whitespace");
            }

            if (string.IsNullOrWhiteSpace(delimiter))
            {
                throw new ArgumentException($"{nameof(delimiter)} can not be null, empty, or whitespace");
            }

            string[] tokens = stringPath.Split(delimiter.ToArray());
            if (tokens.Length == 0)
            {
                throw new ArgumentException($"Was not able to tokenize {stringPath} with {delimiter}");
            }

            Path path = new Path(delimiter);
            foreach (string token in tokens)
            {
                PathToken pathToken = int.TryParse(token, out int index) ? (PathToken)index : (PathToken)token;
                path.ExtendPath(pathToken);
            }

            return path;
        }

        public void ExtendPath(PathToken token)
        {
            this.tokens.Add(token);
        }

        public override string ToString()
        {
            return string.Join(this.delimiter, this.tokens);
        }

        public bool Equals(Path other)
        {
            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            if (this.tokens.Count != other.tokens.Count)
            {
                return false;
            }

            if (!this.delimiter.Equals(other.delimiter))
            {
                return false;
            }

            for (int i = 0; i < this.tokens.Count; ++i)
            {
                if (!this.tokens[i].Equals(other.tokens[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Path path))
            {
                return false;
            }

            return this.Equals(path);
        }

        /// <summary>
        /// Gets the HashCode for a Path Object.
        /// </summary>
        /// <returns>The HashCode for the Path Object.</returns>
        public override int GetHashCode()
        {
            // TODO: Hash functions should be inexpensive to compute.
            // Consider bounding the number tokens to look at.
            int hashCode = 0;
            foreach (object token in this.tokens)
            {
                hashCode ^= token.GetHashCode();
            }

            hashCode ^= this.delimiter.GetHashCode();

            return hashCode;
        }
        //// This class implements IEnumerable so that it can used with foreach syntax.

        /// <summary>
        /// Gets the Enumerator explicitly for object type.
        /// </summary>
        /// <returns>An Enumerator.</returns>
        public IEnumerator<PathToken> GetEnumerator()
        {
            return this.tokens.GetEnumerator();
        }

        /// <summary>
        /// Gets the Enumerator.
        /// </summary>
        /// <returns>An Enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            ////forces use of the non-generic implementation on the Values collection
            return ((IEnumerable)this.tokens).GetEnumerator();
        }

        public int CompareTo(Path other)
        {
            // First sort by the path length
            int compare = this.Length.CompareTo(other.Length);
            if (compare != 0)
            {
                return compare;
            }

            // Break the tie using the actual tokens
            foreach ((PathToken pathToken1, PathToken pathToken2) in this.Zip(other, (first, second) => (first, second)))
            {
                compare = pathToken1.CompareTo(pathToken2);
                if (compare != 0)
                {
                    return compare;
                }
            }

            return compare;
        }
    }
}