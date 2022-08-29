using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable RedundantAssignment
namespace Pancake
{
    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// </remarks>
    public sealed class FrozenDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenDictionary{TKey, TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenDictionary<TKey, TValue> Create(List<TValue> source, Func<TValue, TKey> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenDictionary{TKey, TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenDictionary<TKey, TValue> Create(TValue[] source, Func<TValue, TKey> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        ///  Creates a <see cref="FrozenDictionary{TKey, TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenDictionary<TKey, TValue> Create<TSource>(List<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenDictionary<TKey, TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenDictionary{TKey, TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenDictionary<TKey, TValue> Create<TSource>(TSource[] source, Func<TSource, TKey> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenDictionary<TKey, TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(TKey key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, TKey newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? EqualityComparer<TKey>.Default.GetHashCode(newKey);
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (EqualityComparer<TKey>.Default.Equals(lastEntry.key, newKey))
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[TKey key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<TKey> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(TKey key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            var hash = EqualityComparer<TKey>.Default.GetHashCode(key);
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (EqualityComparer<TKey>.Default.Equals(next.key, key))
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenDictionary{TKey, TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly TKey key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(TKey key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is string specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenStringKeyDictionary<TValue> : IReadOnlyDictionary<string, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenStringKeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenStringKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenStringKeyDictionary<TValue> Create(List<TValue> source, Func<TValue, string> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenStringKeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenStringKeyDictionary<TValue> Create(TValue[] source, Func<TValue, string> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenStringKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenStringKeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, string> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenStringKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }


        /// <summary>
        ///  Creates a <see cref="FrozenStringKeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenStringKeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, string> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenStringKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(string key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, string newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(string key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[string key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<string> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(string key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(string key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenStringKeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly string key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(string key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is sbyte specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenSByteKeyDictionary<TValue> : IReadOnlyDictionary<sbyte, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenSByteKeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenSByteKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenSByteKeyDictionary<TValue> Create(List<TValue> source, Func<TValue, sbyte> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        /// Creates a <see cref="FrozenSByteKeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenSByteKeyDictionary<TValue> Create(TValue[] source, Func<TValue, sbyte> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenSByteKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenSByteKeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, sbyte> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenSByteKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenSByteKeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenSByteKeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, sbyte> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenSByteKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(sbyte key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, sbyte newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(sbyte key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[sbyte key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<sbyte> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(sbyte key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(sbyte key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<sbyte, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenSByteKeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly sbyte key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(sbyte key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is byte specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenByteKeyDictionary<TValue> : IReadOnlyDictionary<byte, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenByteKeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenByteKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenByteKeyDictionary<TValue> Create(List<TValue> source, Func<TValue, byte> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenByteKeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenByteKeyDictionary<TValue> Create(TValue[] source, Func<TValue, byte> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenByteKeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenByteKeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, byte> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenByteKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenByteKeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenByteKeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, byte> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenByteKeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(byte key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, byte newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(byte key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[byte key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<byte> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(byte key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(byte key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<byte, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenByteKeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly byte key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(byte key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is short specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenInt16KeyDictionary<TValue> : IReadOnlyDictionary<short, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenInt16KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenInt16KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt16KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, short> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenInt16KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt16KeyDictionary<TValue> Create(TValue[] source, Func<TValue, short> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenInt16KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt16KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, short> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt16KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenInt16KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt16KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, short> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt16KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(short key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, short newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(short key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[short key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<short> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(short key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(short key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<short, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenInt16KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly short key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(short key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is ushort specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenUInt16KeyDictionary<TValue> : IReadOnlyDictionary<ushort, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenUInt16KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenUInt16KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt16KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, ushort> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenUInt16KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt16KeyDictionary<TValue> Create(TValue[] source, Func<TValue, ushort> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenUInt16KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt16KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, ushort> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt16KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenUInt16KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt16KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, ushort> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt16KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(ushort key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, ushort newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(ushort key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[ushort key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<ushort> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(ushort key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(ushort key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<ushort, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenUInt16KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly ushort key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(ushort key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is int specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenInt32KeyDictionary<TValue> : IReadOnlyDictionary<int, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenInt32KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenInt32KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt32KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, int> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenInt32KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt32KeyDictionary<TValue> Create(TValue[] source, Func<TValue, int> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenInt32KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt32KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, int> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt32KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenInt32KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt32KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, int> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt32KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(int key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, int newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(int key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[int key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<int> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(int key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(int key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<int, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenInt32KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly int key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(int key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is uint specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenUInt32KeyDictionary<TValue> : IReadOnlyDictionary<uint, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenUInt32KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenUInt32KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt32KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, uint> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        /// Creates a <see cref="FrozenUInt32KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt32KeyDictionary<TValue> Create(TValue[] source, Func<TValue, uint> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenUInt32KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt32KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, uint> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt32KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenUInt32KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt32KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, uint> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt32KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(uint key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, uint newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(uint key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[uint key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<uint> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(uint key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(uint key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<uint, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenUInt32KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly uint key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(uint key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is long specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenInt64KeyDictionary<TValue> : IReadOnlyDictionary<long, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenInt64KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenInt64KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt64KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, long> keySelector) => Create(source, keySelector, PassThrough);

        /// <summary>
        /// Creates a <see cref="FrozenInt64KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenInt64KeyDictionary<TValue> Create(TValue[] source, Func<TValue, long> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenInt64KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt64KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, long> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt64KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenInt64KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenInt64KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, long> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenInt64KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(long key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, long newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(long key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[long key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<long> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(long key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(long key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<long, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenInt64KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly long key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(long key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }


    /// <summary>
    /// Provides a read-only dictionary that contents are fixed at the time of instance creation.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <remarks>
    /// Reference:
    /// https://github.com/neuecc/MessagePack-CSharp/blob/master/src/MessagePack.UnityClient/Assets/Scripts/MessagePack/Internal/ThreadsafeTypeKeyHashTable.cs
    /// 
    /// This class is ulong specialized <see cref="FrozenDictionary{TKey, TValue}"/>.
    /// </remarks>
    public sealed class FrozenUInt64KeyDictionary<TValue> : IReadOnlyDictionary<ulong, TValue>
    {
        #region Constants

        private static readonly Func<TValue, TValue> PassThrough = static x => x;

        #endregion


        #region Fields

        private Entry[] _buckets;
        private int _size;
        private readonly float _loadFactor;

        #endregion


        #region Constructors

        /// <summary>
        /// Creates instance.
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="loadFactor"></param>
        private FrozenUInt64KeyDictionary(int bucketSize, float loadFactor)
        {
            this._buckets = (bucketSize == 0) ? Array.Empty<Entry>() : new Entry[bucketSize];
            this._loadFactor = loadFactor;
        }

        #endregion


        #region Create

        /// <summary>
        /// Creates a <see cref="FrozenUInt64KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt64KeyDictionary<TValue> Create(List<TValue> source, Func<TValue, ulong> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        /// Creates a <see cref="FrozenUInt64KeyDictionary{TValue}"/> from an <see cref="Array"/> according to a specified key selector function.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static FrozenUInt64KeyDictionary<TValue> Create(TValue[] source, Func<TValue, ulong> keySelector) => Create(source, keySelector, PassThrough);


        /// <summary>
        ///  Creates a <see cref="FrozenUInt64KeyDictionary{TValue}"/> from an <see cref="List{T}"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt64KeyDictionary<TValue> Create<TSource>(List<TSource> source, Func<TSource, ulong> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt64KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        /// <summary>
        ///  Creates a <see cref="FrozenUInt64KeyDictionary{TValue}"/> from an <see cref="Array"/> according to specified key selector and value selector functions.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="valueSelector"></param>
        /// <returns></returns>
        public static FrozenUInt64KeyDictionary<TValue> Create<TSource>(TSource[] source, Func<TSource, ulong> keySelector, Func<TSource, TValue> valueSelector)
        {
            const int initialSize = 4;
            const float loadFactor = 0.75f;
            var size = source.CountIfMaterialized() ?? initialSize;
            var bucketSize = CalculateCapacity(size, loadFactor);
            var result = new FrozenUInt64KeyDictionary<TValue>(bucketSize, loadFactor);

            foreach (var x in source)
            {
                var key = keySelector(x);
                var value = valueSelector(x);
                if (!result.TryAddInternal(key, value))
                    throw new ArgumentException($"Key was already exists. Key:{key}");
            }

            return result;
        }

        #endregion


        #region Add

        /// <summary>
        /// Add element.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool TryAddInternal(ulong key, TValue value)
        {
            var nextCapacity = CalculateCapacity(this._size + 1, this._loadFactor);
            if (this._buckets.Length < nextCapacity)
            {
                //--- rehash
                var nextBucket = new Entry[nextCapacity];
                for (int i = 0; i < this._buckets.Length; i++)
                {
                    var e = this._buckets[i];
                    while (e is not null)
                    {
                        var newEntry = new Entry(e.key, e.value, e.hash);
                        AddToBuckets(nextBucket, key, newEntry, default);
                        e = e.next;
                    }
                }

                var success = AddToBuckets(nextBucket, key, null, value);
                this._buckets = nextBucket;
                if (success)
                    this._size++;

                return success;
            }
            else
            {
                var success = AddToBuckets(this._buckets, key, null, value);
                if (success)
                    this._size++;

                return success;
            }

            #region Local Functions

            //--- please pass 'key + newEntry' or 'key + value'.
            bool AddToBuckets(Entry[] buckets, ulong newKey, Entry newEntry, TValue value)
            {
                TValue resultingValue;
                var hash = newEntry?.hash ?? newKey.GetHashCode();
                var index = hash & (buckets.Length - 1);
                if (buckets[index] is null)
                {
                    if (newEntry is null)
                    {
                        resultingValue = value;
                        buckets[index] = new Entry(newKey, resultingValue, hash);
                    }
                    else
                    {
                        resultingValue = newEntry.value;
                        buckets[index] = newEntry;
                    }
                }
                else
                {
                    var lastEntry = buckets[index];
                    while (true)
                    {
                        if (lastEntry.key == newKey)
                        {
                            resultingValue = lastEntry.value;
                            return false;
                        }

                        if (lastEntry.next is null)
                        {
                            if (newEntry is null)
                            {
                                resultingValue = value;
                                lastEntry.next = new Entry(newKey, resultingValue, hash);
                            }
                            else
                            {
                                resultingValue = newEntry.value;
                                lastEntry.next = newEntry;
                            }

                            break;
                        }

                        lastEntry = lastEntry.next;
                    }
                }

                return true;
            }

            #endregion
        }


        /// <summary>
        /// Calculates bucket capacity.
        /// </summary>
        /// <param name="collectionSize"></param>
        /// <param name="loadFactor"></param>
        /// <returns></returns>
        private static int CalculateCapacity(int collectionSize, float loadFactor)
        {
            var initialCapacity = (int) (collectionSize / loadFactor);
            var capacity = 1;
            while (capacity < initialCapacity)
                capacity <<= 1;

            if (capacity < 8)
                return 8;

            return capacity;
        }

        #endregion


        #region Get

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary or the default value of the element type.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(ulong key, TValue defaultValue = default) => this.TryGetValue(key, out var value) ? value : defaultValue;

        #endregion


        #region IReadOnlyDictionary<TKey, TValue> implementations

        /// <summary>
        /// Gets the element that has the specified key in the read-only dictionary.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The element that has the specified key in the read-only dictionary.</returns>
        public TValue this[ulong key] => this.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();


        /// <summary>
        /// Gets an enumerable collection that contains the keys in the read-only dictionary.
        /// </summary>
        public IEnumerable<ulong> Keys => throw new NotImplementedException();


        /// <summary>
        /// Gets an enumerable collection that contains the values in the read-only dictionary.
        /// </summary>
        public IEnumerable<TValue> Values => throw new NotImplementedException();


        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count => this._size;


        /// <summary>
        /// Determines whether the read-only dictionary contains an element that has the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>
        /// true if the read-only dictionary contains an element that has the specified key; otherwise, false.
        /// </returns>
        public bool ContainsKey(ulong key) => this.TryGetValue(key, out _);


        /// <summary>
        /// Gets the value that is associated with the specified key.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if the object that implements the <see cref="IReadOnlyDictionary{TKey, TValue}"/> interface contains an element that has the specified key; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(ulong key, out TValue value)
        {
            var hash = key.GetHashCode();
            var index = hash & (this._buckets.Length - 1);
            var next = this._buckets[index];
            while (next is not null)
            {
                if (next.key == key)
                {
                    value = next.value;
                    return true;
                }

                next = next.next;
            }

            value = default;
            return false;
        }


        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<ulong, TValue>> GetEnumerator() => throw new NotImplementedException();


        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion


        #region Inner Classes

        /// <summary>
        /// Represents <see cref="FrozenUInt64KeyDictionary{TValue}"/> entry.
        /// </summary>
        private class Entry
        {
            public readonly ulong key;
            public readonly TValue value;
            public readonly int hash;
            public Entry next;

            public Entry(ulong key, TValue value, int hash)
            {
                this.key = key;
                this.value = value;
                this.hash = hash;
            }
        }

        #endregion
    }
}