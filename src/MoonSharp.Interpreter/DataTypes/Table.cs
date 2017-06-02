using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter.DataStructs;
using System;

namespace MoonSharp.Interpreter
{
	/// <summary>
	/// A class representing a Lua table.
	/// </summary>
	public class Table : RefIdObject, IScriptPrivateResource
	{
		LinkedList<TablePair> m_ValuesList;
		LinkedListIndex<DynValue, TablePair> m_ValueMap;
		LinkedListIndex<string, TablePair> m_StringMap;
		LinkedListIndex<int, TablePair> m_ArrayMap;
		Script m_Owner;

        private LinkedList<TablePair> ValuesList
        {
            get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get ValuesList on dead Table"));
                if (m_ValuesList == null) m_ValuesList = new LinkedList<TablePair>();
                return m_ValuesList;
            }
        }

        int m_InitArray = 0;
		int m_CachedLength = -1;
		bool m_ContainsNilEntries = false;
        bool _isAlive = true;
        public bool isAlive
        {
            get { return _isAlive; }
        }
        public static int count = 0;
        public static int countGC = 0;
        public static int countPrime = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="owner">The owner script.</param>
        public Table(Script owner)
		{
			m_Owner = owner;

            if (owner != null)
            {
                owner.RegisterTable(this);
                count++;
            }
            else
            {
                countPrime++;
            }
            countGC++;
        }

        public static void Kill(ref Table tokill)
        {
            _Kill(tokill);
            tokill = null;
        }

        private static void _Kill(Table tokill)
        {
            if (tokill != null && tokill._isAlive)
            {
                if (tokill.m_Owner != null)
                {
                    count--;
                }
                else
                {
                    countPrime--;
                }
                tokill.Clear();
                tokill.m_ValuesList = null;
                tokill.m_ValueMap = null;
                tokill.m_StringMap = null;
                tokill.m_ArrayMap = null;
                tokill.m_Owner = null;
                tokill._isAlive = false;
            }
        }

        ~Table()
        {
            _Kill(this);
            countGC--;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="arrayValues">The values for the "array-like" part of the table.</param>
        public Table(Script owner, IList<DynValue> arrayValues)
            : this(owner)
        {
            for (int i = 0; i < arrayValues.Count; i++)
            {
                this.Set(DynValue.NewNumber(i + 1), arrayValues[i]);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="hashValue">The values for the "hash-like" part of the table.</param>
        public Table(Script owner, ICollection<KeyValuePair<string, DynValue>> hashValue)
            : this(owner)
        {
            foreach (KeyValuePair<string, DynValue> kvp in hashValue)
            {
                this.Set(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Gets the script owning this resource.
        /// </summary>
        public Script OwnerScript
		{
			get { return m_Owner; }
		}

        public void MakePrime()
        {
            if (m_Owner != null)
            {
                m_Owner.DeregisterTable(this);
                m_Owner = null;
                count--;
                countPrime++;
                foreach (TablePair kvp in Pairs)
                {
                    DynValue value = kvp.Value;
                    if (value.IsValid)
                    {
                        if (value.Type == DataType.Table)
                        {
                            value.Table.MakePrime();
                        }
                    }
                }
            }
        }

		/// <summary>
		/// Removes all items from the Table.
		/// </summary>
		public void Clear()
		{
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Clear on dead Table"));
            if (m_ValuesList != null) ValuesList.Clear();
			if (m_StringMap != null) m_StringMap.Clear();
            if (m_ArrayMap != null) m_ArrayMap.Clear();
            if (m_ValueMap != null) m_ValueMap.Clear();
            m_CachedLength = -1;
		}

		/// <summary>
		/// Gets the integral key from a double.
		/// </summary>
		private int GetIntegralKey(double d)
		{

            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to GetIntegralKey on dead Table"));
            int v = ((int)d);

			if (d >= 1.0 && d == v)
				return v;

			return -1;
		}

		/// <summary>
		/// Gets or sets the 
		/// <see cref="System.Object" /> with the specified key(s).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object" />.
		/// </value>
		/// <param name="keys">The keys to access the table and subtables</param>
		public object this[params object[] keys]
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get index on dead Table"));
                return Get(keys).ToObject<object>();
			}
			set
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to set index on dead Table"));
                Set(keys, DynValue.FromObject(OwnerScript, value));
			}
		}

		/// <summary>
		/// Gets or sets the <see cref="System.Object"/> with the specified key(s).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object"/>.
		/// </value>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public object this[object key]
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get index on dead Table"));
                return Get(key).ToObject<object>();
			}
			set
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to set inedx on dead Table"));
                Set(key, DynValue.FromObject(OwnerScript, value));
			}
		}

		private Table ResolveMultipleKeys(object[] keys, out object key)
		{
            //Contract.Ensures(Contract.Result<Table>() != null);
            //Contract.Requires(keys != null);

            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to ResolveMultipleKeys on dead Table"));
            Table t = this;
			key = (keys.Length > 0) ? keys[0] : null;

			for (int i = 1; i < keys.Length; ++i)
			{
				DynValue vt = t.RawGet(key);

				if (!vt.IsValid)
					throw new ScriptRuntimeException("Key '{0}' did not point to anything");

				if (vt.Type != DataType.Table)
					throw new ScriptRuntimeException("Key '{0}' did not point to a table");

				t = vt.Table;
				key = keys[i];
			}

			return t;
		}

		/// <summary>
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(DynValue value)
		{
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Append on dead Table"));
            this.CheckScriptOwnership(value);

            if (m_ArrayMap == null) m_ArrayMap = new LinkedListIndex<int, TablePair>(ValuesList);
            PerformTableSet(m_ArrayMap, Length + 1, DynValue.NewNumber(Length + 1), value, true, Length + 1);
		}

		#region Set

		private void PerformTableSet<T>(LinkedListIndex<T, TablePair> listIndex, T key, DynValue keyDynValue, DynValue value, bool isNumber, int appendKey)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to PerformTableSet on dead Table"));
            TablePair prev = listIndex.Set(key, new TablePair(keyDynValue, value));

			// If this is an insert, we can invalidate all iterators and collect dead keys
			if (m_ContainsNilEntries && value.IsNotNil() && (prev.Value.IsNil()))
			{
				CollectDeadKeys();
			}
			// If this value is nil (and we didn't collect), set that there are nil entries, and invalidate array len cache
			else if (value.IsNil())
			{
				m_ContainsNilEntries = true;

				if (isNumber)
					m_CachedLength = -1;
			}
			else if (isNumber)
			{
				// If this is an array insert, we might have to invalidate the array length
				if (prev.Value.IsNilOrNan())
				{
					// If this is an array append, let's check the next element before blindly invalidating
					if (appendKey >= 0)
					{
                        if (m_ArrayMap == null)
                        {
                            m_CachedLength += 1;
                        }
                        else
                        {
                            LinkedListNode<TablePair> next = m_ArrayMap.Find(appendKey + 1);
                            if (next == null || next.Value.Value.IsNil())
                            {
                                m_CachedLength += 1;
                            }
                            else
                            {
                                m_CachedLength = -1;
                            }
                        }
					}
					else
					{
						m_CachedLength = -1;
					}
				}
			}
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(string key, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            if (key == null)
				throw ScriptRuntimeException.TableIndexIsNil();

			this.CheckScriptOwnership(value);

            if (m_StringMap == null) m_StringMap = new LinkedListIndex<string, TablePair>(ValuesList);
            PerformTableSet(m_StringMap, key, DynValue.NewString(key), value, false, -1);
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(int key, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            this.CheckScriptOwnership(value);

            if (m_ArrayMap == null) m_ArrayMap = new LinkedListIndex<int, TablePair>(ValuesList);
            PerformTableSet(m_ArrayMap, key, DynValue.NewNumber(key), value, true, -1);
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(DynValue key, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            if (key.IsNilOrNan())
			{
				if (key.IsNil())
					throw ScriptRuntimeException.TableIndexIsNil();
				else
					throw ScriptRuntimeException.TableIndexIsNaN();
			}

			if (key.Type == DataType.String)
			{
				Set(key.String, value);
				return;
			}

			if (key.Type == DataType.Number)
			{
				int idx = GetIntegralKey(key.Number);

				if (idx > 0)
				{
					Set(idx, value);
					return;
				}
			}

			this.CheckScriptOwnership(key);
			this.CheckScriptOwnership(value);

            if (m_ValueMap == null) m_ValueMap = new LinkedListIndex<DynValue, TablePair>(ValuesList);
            PerformTableSet(m_ValueMap, key, key, value, false, -1);
		}

		/// <summary>
		/// Sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(object key, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            if (key == null)
				throw ScriptRuntimeException.TableIndexIsNil();

			if (key is string)
				Set((string)key, value);
			else if (key is int)
				Set((int)key, value);
			else
				Set(DynValue.FromObject(OwnerScript, key), value);
		}

        /// <summary>
		/// Sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set<T>(T key, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            if (key == null)
                throw ScriptRuntimeException.TableIndexIsNil();

            Set(DynValue.FromObject(OwnerScript, key), value);
        }

        /// <summary>
        /// Sets the value associated with the specified keys.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <param name="key">The keys.</param>
        /// <param name="value">The value.</param>
        public void Set(object[] keys, DynValue value)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Set on dead Table"));
            if (keys == null || keys.Length <= 0)
				throw ScriptRuntimeException.TableIndexIsNil();

			object key;
			ResolveMultipleKeys(keys, out key).Set(key, value);
		}

        #endregion

        #region Get

        /// <summary>
        /// Gets the table associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public Table GetTable(string key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to GetTable on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            return RawGet(key).Table ?? null;
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public DynValue Get(string key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(key);
            return d.IsValid ? d : DynValue.Nil;
        }

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(int key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(key);
            return d.IsValid ? d : DynValue.Nil;
        }

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(DynValue key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(key);
            return d.IsValid ? d : DynValue.Nil;
        }

		/// <summary>
		/// Gets the value associated with the specified key.
		/// (expressed as a <see cref="System.Object"/>).
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(object key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(key);
            return d.IsValid ? d : DynValue.Nil;
        }

        /// <summary>
		/// Gets the value associated with the specified key.
		/// (expressed as a <see cref="System.Object"/>).
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get<T>(T key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(key);
            return  d.IsValid ? d : DynValue.Nil;
        }

        /// <summary>
        /// Gets the value associated with the specified keys (expressed as an 
        /// array of <see cref="System.Object"/>).
        /// This will marshall CLR and MoonSharp objects in the best possible way.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <param name="keys">The keys to access the table and subtables</param>
        public DynValue Get(params object[] keys)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Get on dead Table"));
            //Contract.Ensures(Contract.Result<DynValue>() != null);
            var d = RawGet(keys);
            return d.IsValid ? d : DynValue.Nil;
        }

		#endregion

		#region RawGet

		private static DynValue RawGetValue(LinkedListNode<TablePair> linkedListNode)
        {
            return (linkedListNode != null) ? linkedListNode.Value.Value : DynValue.Invalid;
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(string key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            if (m_StringMap == null) return DynValue.Invalid;
            return RawGetValue(m_StringMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(int key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            if (m_ArrayMap == null) return DynValue.Invalid;
            return RawGetValue(m_ArrayMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(DynValue key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            if (key.Type == DataType.String)
				return RawGet(key.String);

			if (key.Type == DataType.Number)
			{
				int idx = GetIntegralKey(key.Number);
				if (idx > 0)
					return RawGet(idx);
			}
            if (m_ValueMap == null) return DynValue.Invalid;
			return RawGetValue(m_ValueMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(object key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            if (key == null)
				return DynValue.Invalid;

			if (key is string)
				return RawGet((string)key);

			if (key is int)
				return RawGet((int)key);

			return RawGet(DynValue.FromObject(OwnerScript, key));
		}

        /// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet<T>(T key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            return RawGet(DynValue.FromObject(OwnerScript, key));
        }

        /// <summary>
        /// Gets the value associated with the specified keys (expressed as an
        /// array of <see cref="System.Object"/>).
        /// This will marshall CLR and MoonSharp objects in the best possible way.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <param name="keys">The keys to access the table and subtables</param>
        public DynValue RawGet(params object[] keys)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to RawGet on dead Table"));
            if (keys == null || keys.Length <= 0)
				return DynValue.Invalid;

			object key;
			return ResolveMultipleKeys(keys, out key).RawGet(key);
		}

		#endregion

		#region Remove

		private bool PerformTableRemove<T>(LinkedListIndex<T, TablePair> listIndex, T key, bool isNumber)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to PerformTableRemove on dead Table"));
            var removed = listIndex.Remove(key);

			if (removed && isNumber)
			{
				m_CachedLength = -1;
			}

			return removed;
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(string key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Remove on dead Table"));
            if (m_StringMap == null) return false;
            return PerformTableRemove(m_StringMap, key, false);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(int key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Remove on dead Table"));
            if (m_ArrayMap == null) return false;
            return PerformTableRemove(m_ArrayMap, key, true);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(DynValue key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Remove on dead Table"));
            if (key.Type == DataType.String)
				return Remove(key.String);

			if (key.Type == DataType.Number)
			{
				int idx = GetIntegralKey(key.Number);
				if (idx > 0)
					return Remove(idx);
			}

            if (m_ValueMap == null) return false;
            return PerformTableRemove(m_ValueMap, key, false);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(object key)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Remove on dead Table"));
            if (key is string)
				return Remove((string)key);

			if (key is int)
				return Remove((int)key);

			return Remove(DynValue.FromObject(OwnerScript, key));
		}

		/// <summary>
		/// Remove the value associated with the specified keys from the table.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(params object[] keys)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to Remove on dead Table"));
            if (keys == null || keys.Length <= 0)
				return false;

			object key;
			return ResolveMultipleKeys(keys, out key).Remove(key);
		}

		#endregion

		/// <summary>
		/// Collects the dead keys. This frees up memory but invalidates pending iterators.
		/// It's called automatically internally when the semantics of Lua tables allow, but can be forced
		/// externally if it's known that no iterators are pending.
		/// </summary>
		public void CollectDeadKeys()
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to CollectDeadKeys on dead Table"));
            if (m_ValuesList != null)
            {
                for (LinkedListNode<TablePair> node = ValuesList.First; node != null; node = node.Next)
                {
                    if (node.Value.Value.IsNil())
                    {
                        Remove(node.Value.Key);
                    }
                }
            }

			m_ContainsNilEntries = false;
			m_CachedLength = -1;
		}


		/// <summary>
		/// Returns the next pair from a value
		/// </summary>
		public TablePair? NextKey(DynValue v)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to NextKey on dead Table"));
            if (v.IsNil())
            {
                if (m_ValuesList == null) return TablePair.Nil;
                LinkedListNode<TablePair> node = ValuesList.First;

				if (node == null)
					return TablePair.Nil;
				else
				{
					if (node.Value.Value.IsNil())
						return NextKey(node.Value.Key);
					else
						return node.Value;
				}
			}

			if (v.Type == DataType.String)
            {
                if (m_StringMap == null) return null;
                return GetNextOf(m_StringMap.Find(v.String));
			}

			if (v.Type == DataType.Number)
			{
				int idx = GetIntegralKey(v.Number);

				if (idx > 0)
                {
                    if (m_ArrayMap == null) return null;
                    return GetNextOf(m_ArrayMap.Find(idx));
				}
			}

            if (m_ValueMap == null) return null;
            return GetNextOf(m_ValueMap.Find(v));
		}

		private TablePair? GetNextOf(LinkedListNode<TablePair> linkedListNode)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to GetNextOf on dead Table"));
            while (true)
			{
				if (linkedListNode == null)
					return null;

				if (linkedListNode.Next == null)
					return TablePair.Nil;

				linkedListNode = linkedListNode.Next;

				if (!linkedListNode.Value.Value.IsNil())
					return linkedListNode.Value;
			}
		}


		/// <summary>
		/// Gets the length of the "array part".
		/// </summary>
		public int Length
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get Length on dead Table"));
                if (m_CachedLength < 0)
				{
					m_CachedLength = 0;
                    if (m_ArrayMap != null)
                    {
                        for (int i = 1; m_ArrayMap.ContainsKey(i) && !m_ArrayMap.Find(i).Value.Value.IsNil(); i++)
                            m_CachedLength = i;
                    }
				}

				return m_CachedLength;
			}
		}

		internal void InitNextArrayKeys(DynValue val, bool lastpos)
        {
            if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to InitNextArrayKeys on dead Table"));
            if (val.Type == DataType.Tuple && lastpos)
			{
				foreach (DynValue v in val.Tuple)
					InitNextArrayKeys(v, true);
			}
			else
			{
				Set(++m_InitArray, val.ToScalar());
			}
		}

		/// <summary>
		/// Gets the meta-table associated with this instance.
		/// </summary>
		public Table MetaTable
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get MetaTable on dead Table"));
                return m_MetaTable;
            }
			set
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to set MetaTable on dead Table"));
                this.CheckScriptOwnership(m_MetaTable); m_MetaTable = value;
            }
		}
		private Table m_MetaTable;



		/// <summary>
		/// Enumerates the key/value pairs.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TablePair> Pairs
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get Pairs on dead Table"));
                return m_ValuesList != null ? ValuesList.Select(n => new TablePair(n.Key, n.Value)) : Enumerable.Empty<TablePair>();
			}
		}

        

		/// <summary>
		/// Enumerates the keys.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Keys
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get Keys on dead Table"));
                return m_ValuesList != null ? ValuesList.Select(n => n.Key) : Enumerable.Empty<DynValue>();
			}
		}

		/// <summary>
		/// Enumerates the values
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Values
		{
			get
            {
                if (!_isAlive) throw new InvalidOperationException(string.Format("Attempting to get Values on dead Table"));
                return m_ValuesList != null ? ValuesList.Select(n => n.Value) : Enumerable.Empty<DynValue>();
			}
		}
	}
}
