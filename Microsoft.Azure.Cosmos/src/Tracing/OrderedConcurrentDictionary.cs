// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A thread-safe dictionary implementation that preserves insertion order.
    /// This class combines a ConcurrentDictionary for thread safety with an 
    /// internal list to preserve insertion order.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    internal class OrderedConcurrentDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        // Use ConcurrentDictionary for thread-safe storage
        private readonly ConcurrentDictionary<TKey, TValue> dictionary;
        
        // Use ConcurrentQueue to maintain insertion order in a thread-safe manner
        private readonly ConcurrentQueue<TKey> orderQueue;

        public OrderedConcurrentDictionary()
        {
            this.dictionary = new ConcurrentDictionary<TKey, TValue>();
            this.orderQueue = new ConcurrentQueue<TKey>();
        }

        /// <summary>
        /// Gets the collection of keys in this dictionary.
        /// </summary>
        public IEnumerable<TKey> Keys => this.orderQueue.ToArray();

        /// <summary>
        /// Gets the collection of values in this dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => this.Keys.Select(key => this.dictionary[key]);

        /// <summary>
        /// Gets the number of key/value pairs in the dictionary.
        /// </summary>
        public int Count => this.dictionary.Count;

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The value associated with the specified key.</returns>
        public TValue this[TKey key] => this.dictionary[key];

        /// <summary>
        /// Adds a new key and value to the dictionary.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>true if the key/value pair was added successfully; otherwise, false.</returns>
        public bool TryAdd(TKey key, TValue value)
        {
            bool added = this.dictionary.TryAdd(key, value);
            
            if (added)
            {
                // Only add to order queue if it was actually added to the dictionary
                this.orderQueue.Enqueue(key);
            }
            
            return added;
        }

        /// <summary>
        /// Adds a key/value pair if the key does not exist, or updates a key/value pair 
        /// if the key already exists.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to add or update.</param>
        /// <param name="valueFactory">Function to generate a new value from an existing key and value.</param>
        /// <returns>The new value for the key.</returns>
        public TValue AddOrUpdate(TKey key, TValue value, Func<TKey, TValue, TValue> valueFactory)
        {
            // Check if the key doesn't exist before adding to maintain order
            if (!this.dictionary.ContainsKey(key))
            {
                // Try to add the key first before enqueueing
                if (this.dictionary.TryAdd(key, value))
                {
                    this.orderQueue.Enqueue(key);
                    return value;
                }
            }
            
            // If key already exists or add failed, update it
            return this.dictionary.AddOrUpdate(key, value, valueFactory);
        }

        /// <summary>
        /// Determines whether the dictionary contains the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>true if the dictionary contains the specified key; otherwise, false.</returns>
        public bool ContainsKey(TKey key) => this.dictionary.ContainsKey(key);

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, 
        /// if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns>true if the dictionary contains the specified key; otherwise, false.</returns>
        public bool TryGetValue(TKey key, out TValue value) => this.dictionary.TryGetValue(key, out value);

        /// <summary>
        /// Returns an enumerator that iterates through the dictionary in insertion order.
        /// </summary>
        /// <returns>An enumerator for the dictionary in insertion order.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            // Use the order queue to return items in insertion order
            foreach (TKey key in this.orderQueue)
            {
                if (this.dictionary.TryGetValue(key, out TValue value))
                {
                    yield return new KeyValuePair<TKey, TValue>(key, value);
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the dictionary.
        /// </summary>
        /// <returns>An enumerator for the dictionary.</returns>
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}