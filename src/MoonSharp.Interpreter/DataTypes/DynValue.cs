﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter.Interop.Converters;
using System.Threading;

namespace MoonSharp.Interpreter
{

    /// <summary>
    /// A class representing a value in a Lua/MoonSharp script.
    /// </summary>
    public class DynValue
	{
        public static int INSTANCE_AMOUNT;
        public const int MAX_POOL_SIZE = 100000;

        private static int s_RefIDCounter = 0;
        private static Stack<DynValue> _pool = new Stack<DynValue>(MAX_POOL_SIZE);


		private int m_RefID = Interlocked.Increment(ref s_RefIDCounter);
		private int m_HashCode = -1;

		private bool m_ReadOnly;
		private double m_Number;
		private object m_Object;
		private DataType m_Type;


		/// <summary>
		/// Gets a unique reference identifier. This is guaranteed to be unique only for dynvalues created in a single thread as it's not thread-safe.
		/// </summary>
		public int ReferenceID { get { return m_RefID; } }

		/// <summary>
		/// Gets the type of the value.
		/// </summary>
		public DataType Type { get { return m_Type; } }
		/// <summary>
		/// Gets the function (valid only if the <see cref="Type"/> is <see cref="DataType.Function"/>)
		/// </summary>
		public Closure Function { get { return m_Object as Closure; } }
		/// <summary>
		/// Gets the numeric value (valid only if the <see cref="Type"/> is <see cref="DataType.Number"/>)
		/// </summary>
		public double Number { get { return m_Number; } }
		/// <summary>
		/// Gets the values in the tuple (valid only if the <see cref="Type"/> is Tuple).
		/// This field is currently also used to hold arguments in values whose <see cref="Type"/> is <see cref="DataType.TailCallRequest"/>.
		/// </summary>
		public DynValue[] Tuple { get { return m_Object as DynValue[]; } }
		/// <summary>
		/// Gets the coroutine handle. (valid only if the <see cref="Type"/> is Thread).
		/// </summary>
		public Coroutine Coroutine { get { return m_Object as Coroutine; } }
		/// <summary>
		/// Gets the table (valid only if the <see cref="Type"/> is <see cref="DataType.Table"/>)
		/// </summary>
		public Table Table { get { return m_Object as Table; } }
		/// <summary>
		/// Gets the boolean value (valid only if the <see cref="Type"/> is <see cref="DataType.Boolean"/>)
		/// </summary>
		public bool Boolean { get { return Number != 0; } }
		/// <summary>
		/// Gets the string value (valid only if the <see cref="Type"/> is <see cref="DataType.String"/>)
		/// </summary>
		public string String { get { return m_Object as string; } }
		/// <summary>
		/// Gets the CLR callback (valid only if the <see cref="Type"/> is <see cref="DataType.ClrFunction"/>)
		/// </summary>
		public CallbackFunction Callback { get { return m_Object as CallbackFunction; } }
		/// <summary>
		/// Gets the tail call data.
		/// </summary>
		public TailCallData TailCallData { get { return m_Object as TailCallData; } }
		/// <summary>
		/// Gets the yield request data.
		/// </summary>
		public YieldRequest YieldRequest { get { return m_Object as YieldRequest; } }
		/// <summary>
		/// Gets the tail call data.
		/// </summary>
		public IUserData UserData { get { return m_Object as IUserData; } }

		/// <summary>
		/// Returns true if this instance is write protected.
		/// </summary>
		public bool ReadOnly { get { return m_ReadOnly; } }

	    internal static DynValue Request()
	    {
	        DynValue d;
            lock (_pool)
            {
                if (_pool.Count > 0)
	            {
                     d = _pool.Pop();
                    GC.ReRegisterForFinalize(d);
	            }
                else
                { 
                    d = new DynValue();
                    INSTANCE_AMOUNT++;
                }
            }
            return d;
	    }

	    ~DynValue()
	    {
		    m_HashCode = -1;
            m_ReadOnly = false;
		    m_Number = 0;
		    m_Object = null;
		    m_Type = 0;
            lock (_pool)
            {
                if (_pool.Count < MAX_POOL_SIZE)
                    _pool.Push(this);
            }
        }

        /// <summary>
        /// Creates a new writable value initialized to Nil.
        /// </summary>
        public static DynValue NewNil()
		{
			return Request();
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified boolean.
		/// </summary>
		public static DynValue NewBoolean(bool v)
		{
		    var d = Request();
            d.m_Number = v ? 1 : 0;
            d.m_Type = DataType.Boolean;
		    return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified number.
		/// </summary>
		public static DynValue NewNumber(double num)
		{
            var d = Request();
            d.m_Number = num;
            d.m_Type = DataType.Number;
		    d.m_HashCode = -1;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified string.
		/// </summary>
		public static DynValue NewString(string str)
		{
            var d = Request();
            d.m_Object = str;
            d.m_Type = DataType.String;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified StringBuilder.
		/// </summary>
		public static DynValue NewString(StringBuilder sb)
		{
            var d = Request();
            d.m_Object = sb.ToString();
            d.m_Type = DataType.String;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified string using String.Format like syntax
		/// </summary>
		public static DynValue NewString(string format, params object[] args)
		{
            var d = Request();
            d.m_Object = string.Format(format, args);
            d.m_Type = DataType.String;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified coroutine.
		/// Internal use only, for external use, see Script.CoroutineCreate
		/// </summary>
		/// <param name="coroutine">The coroutine object.</param>
		/// <returns></returns>
		public static DynValue NewCoroutine(Coroutine coroutine)
		{
            var d = Request();
            d.m_Object = coroutine;
            d.m_Type = DataType.Thread;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified closure (function).
		/// </summary>
		public static DynValue NewClosure(Closure function)
		{
            var d = Request();
            d.m_Object = function;
            d.m_Type = DataType.Function;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified CLR callback.
		/// </summary>
		public static DynValue NewCallback(Func<ScriptExecutionContext, CallbackArguments, DynValue> callBack, string name = null)
		{
            var d = Request();
            d.m_Object = new CallbackFunction(callBack, name);
            d.m_Type = DataType.ClrFunction;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified CLR callback.
		/// See also CallbackFunction.FromDelegate and CallbackFunction.FromMethodInfo factory methods.
		/// </summary>
		public static DynValue NewCallback(CallbackFunction function)
		{
            var d = Request();
            d.m_Object = function;
            d.m_Type = DataType.ClrFunction;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to the specified table.
		/// </summary>
		public static DynValue NewTable(Table table)
		{
            var d = Request();
            d.m_Object = table;
            d.m_Type = DataType.Table;
            return d;
		}

		/// <summary>
		/// Creates a new writable value initialized to an empty prime table (a 
		/// prime table is a table made only of numbers, strings, booleans and other
		/// prime tables).
		/// </summary>
		public static DynValue NewPrimeTable()
		{
			return NewTable(new Table(null));
		}

		/// <summary>
		/// Creates a new writable value initialized to an empty table.
		/// </summary>
		public static DynValue NewTable(Script script)
		{
			return NewTable(new Table(script));
		}

		/// <summary>
		/// Creates a new writable value initialized to with array contents.
		/// </summary>
		public static DynValue NewTable(Script script, params DynValue[] arrayValues)
		{
			return NewTable(new Table(script, arrayValues));
		}

        /// <summary>
        /// Creates a new writable value initialized to with array contents.
        /// </summary>
        public static DynValue NewTable(Script script, List<DynValue> arrayValues)
        {
            return NewTable(new Table(script, arrayValues));
        }

		/// <summary>
		/// Creates a new request for a tail call. This is the preferred way to execute Lua/MoonSharp code from a callback,
		/// although it's not always possible to use it. When a function (callback or script closure) returns a
		/// TailCallRequest, the bytecode processor immediately executes the function contained in the request.
		/// By executing script in this way, a callback function ensures it's not on the stack anymore and thus a number
		/// of functionality (state savings, coroutines, etc) keeps working at full power.
		/// </summary>
		/// <param name="tailFn">The function to be called.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public static DynValue NewTailCallReq(DynValue tailFn, params DynValue[] args)
		{
            var d = Request();
            d.m_Object = new TailCallData()
            {
                Args = args,
                Function = tailFn,
            };
            d.m_Type = DataType.TailCallRequest;
            return d;
    	}

		/// <summary>
		/// Creates a new request for a tail call. This is the preferred way to execute Lua/MoonSharp code from a callback,
		/// although it's not always possible to use it. When a function (callback or script closure) returns a
		/// TailCallRequest, the bytecode processor immediately executes the function contained in the request.
		/// By executing script in this way, a callback function ensures it's not on the stack anymore and thus a number
		/// of functionality (state savings, coroutines, etc) keeps working at full power.
		/// </summary>
		/// <param name="tailCallData">The data for the tail call.</param>
		/// <returns></returns>
		public static DynValue NewTailCallReq(TailCallData tailCallData)
		{
            var d = Request();
            d.m_Object = tailCallData;
            d.m_Type = DataType.TailCallRequest;
		    return d;
		}



		/// <summary>
		/// Creates a new request for a yield of the current coroutine.
		/// </summary>
		/// <param name="args">The yield argumenst.</param>
		/// <returns></returns>
		public static DynValue NewYieldReq(DynValue[] args)
		{
            var d = Request();
            d.m_Object = new YieldRequest() { ReturnValues = args };
            d.m_Type = DataType.YieldRequest;
            return d;
		}

		/// <summary>
		/// Creates a new request for a yield of the current coroutine.
		/// </summary>
		/// <param name="args">The yield argumenst.</param>
		/// <returns></returns>
		internal static DynValue NewForcedYieldReq()
		{
            var d = Request();
            d.m_Object = new YieldRequest() { Forced = true };
            d.m_Type = DataType.YieldRequest;
            return d;
        }

		/// <summary>
		/// Creates a new tuple initialized to the specified values.
		/// </summary>
		public static DynValue NewTuple(params DynValue[] values)
		{
            if (values.Length == 0)
                return NewNil();

            if (values.Length == 1)
                return values[0];

            var d = Request();

            d.m_Object = values;
            d.m_Type = DataType.Tuple;

            return d;
		}

		/// <summary>
		/// Creates a new tuple initialized to the specified values - which can be potentially other tuples
		/// </summary>
		public static DynValue NewTupleNested(params DynValue[] values)
		{
			if (!values.Any(v => v.Type == DataType.Tuple))
				return NewTuple(values);

			if (values.Length == 1)
				return values[0];

			List<DynValue> vals = new List<DynValue>();

			foreach (var v in values)
			{
				if (v.Type == DataType.Tuple)
					vals.AddRange(v.Tuple);
				else
					vals.Add(v);
			}

            var d = Request();
		    d.m_Object = vals.ToArray();
		    d.m_Type = DataType.Tuple;
		    return d;
		}


		/// <summary>
		/// Creates a new userdata value
		/// </summary>
		public static DynValue NewUserData(IUserData userData)
		{
            var d = Request();
            d.m_Object = userData;
            d.m_Type = DataType.UserData;
            return d;
		}

		/// <summary>
		/// Returns this value as readonly - eventually cloning it in the process if it isn't readonly to start with.
		/// </summary>
		public DynValue AsReadOnly()
		{
			if (ReadOnly)
				return this;
			else
			{
				return Clone(true);
			}
		}

		/// <summary>
		/// Clones this instance.
		/// </summary>
		/// <returns></returns>
		public DynValue Clone()
		{
			return Clone(this.ReadOnly);
		}

		/// <summary>
		/// Clones this instance, overriding the "readonly" status.
		/// </summary>
		/// <param name="readOnly">if set to <c>true</c> the new instance is set as readonly, or writeable otherwise.</param>
		/// <returns></returns>
		public DynValue Clone(bool readOnly)
		{
			DynValue v = Request();
			v.m_Object = this.m_Object;
			v.m_Number = this.m_Number;
			v.m_HashCode = this.m_HashCode;
			v.m_Type = this.m_Type;
			v.m_ReadOnly = readOnly;
			return v;
		}

		/// <summary>
		/// Clones this instance, returning a writable copy.
		/// </summary>
		/// <exception cref="System.ArgumentException">Can't clone Symbol values</exception>
		public DynValue CloneAsWritable()
		{
			return Clone(false);
		}


		/// <summary>
		/// A preinitialized, readonly instance, equaling Void
		/// </summary>
		public static DynValue Void { get; private set; }
		/// <summary>
		/// A preinitialized, readonly instance, equaling Nil
		/// </summary>
		public static DynValue Nil { get; private set; }
		/// <summary>
		/// A preinitialized, readonly instance, equaling True
		/// </summary>
		public static DynValue True { get; private set; }
		/// <summary>
		/// A preinitialized, readonly instance, equaling False
		/// </summary>
		public static DynValue False { get; private set; }


		static DynValue()
		{
            Nil = Request();
		    Nil.m_Type = DataType.Nil;
            Nil = Nil.AsReadOnly();

            Void = Request();
            Void.m_Type = DataType.Void;
            Void = Void.AsReadOnly();
			True = NewBoolean(true).AsReadOnly();
			False = NewBoolean(false).AsReadOnly();

            new System.Threading.Thread(() => WarnDynValueCache()).Start();
        }

        private static void WarnDynValueCache()
	    {
            INSTANCE_AMOUNT = MAX_POOL_SIZE;
            for (int i = MAX_POOL_SIZE; i > 0; i--)
            {
                var d = new DynValue();
                GC.SuppressFinalize(d);
                lock (_pool)
                {
                    _pool.Push(d);
                }
            }
        }


		/// <summary>
		/// Returns a string which is what it's expected to be output by the print function applied to this value.
		/// </summary>
		public string ToPrintString()
		{
			if (this.m_Object != null && this.m_Object is RefIdObject)
			{
				RefIdObject refid = (RefIdObject)m_Object;

				string typeString = this.Type.ToLuaTypeString();

				if (m_Object is IUserData)
				{
					IUserData ud = (IUserData)m_Object;
					string str = ud.AsString();
					if (str != null)
						return str;
				}

				return refid.FormatTypeString(typeString);
			}

			switch (Type)
			{
				case DataType.String:
					return String;
				case DataType.Tuple:
					return string.Join("\t", Tuple.Select(t => t.ToPrintString()).ToArray());
				case DataType.TailCallRequest:
					return "(TailCallRequest -- INTERNAL!)";
				case DataType.YieldRequest:
					return "(YieldRequest -- INTERNAL!)";
				default:
					return ToString();
			}
		}

		/// <summary>
		/// Returns a string which is what it's expected to be output by debuggers.
		/// </summary>
		public string ToDebugPrintString()
		{
			if (this.m_Object != null && this.m_Object is RefIdObject)
			{
				RefIdObject refid = (RefIdObject)m_Object;

				string typeString = this.Type.ToLuaTypeString();

				if (m_Object is IUserData)
				{
					IUserData ud = (IUserData)m_Object;
					string str = ud.Descriptor.AsString(ud.TryGet());
					if (str != null)
						return str;
				}

				return refid.FormatTypeString(typeString);
			}

			switch (Type)
			{
				case DataType.Tuple:
					return string.Join("\t", Tuple.Select(t => t.ToPrintString()).ToArray());
				case DataType.TailCallRequest:
					return "(TailCallRequest)";
				case DataType.YieldRequest:
					return "(YieldRequest)";
				default:
					return ToString();
			}
		}


		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			switch (Type)
			{
				case DataType.Void:
					return "void";
				case DataType.Nil:
					return "nil";
				case DataType.Boolean:
					return Boolean.ToString().ToLower();
				case DataType.Number:
					return Number.ToString(CultureInfo.InvariantCulture);
				case DataType.String:
					return "\"" + String + "\"";
				case DataType.Function:
					return string.Format("(Function {0:X8})", Function.EntryPointByteCodeLocation);
				case DataType.ClrFunction:
					return string.Format("(Function CLR)", Function);
				case DataType.Table:
					return "(Table)";
				case DataType.Tuple:
					return string.Join(", ", Tuple.Select(t => t.ToString()).ToArray());
				case DataType.TailCallRequest:
					return "Tail:(" + string.Join(", ", Tuple.Select(t => t.ToString()).ToArray()) + ")";
				case DataType.UserData:
					return "(UserData)";
				case DataType.Thread:
					return string.Format("(Coroutine {0:X8})", this.Coroutine.ReferenceID);
				default:
					return "(???)";
			}
		}

		/// <summary>
		/// Returns a hash code for this instance.
		/// </summary>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
		/// </returns>
		public override int GetHashCode()
		{
			if (m_HashCode != -1)
				return m_HashCode;

			int baseValue = ((int)(Type)) << 27;

			switch (Type)
			{
				case DataType.Void:
				case DataType.Nil:
					m_HashCode = 0;
					break;
				case DataType.Boolean:
					m_HashCode = Boolean ? 1 : 2;
					break;
				case DataType.Number:
					m_HashCode = baseValue ^ Number.GetHashCode();
					break;
				case DataType.String:
					m_HashCode = baseValue ^ String.GetHashCode();
					break;
				case DataType.Function:
					m_HashCode = baseValue ^ Function.GetHashCode();
					break;
				case DataType.ClrFunction:
					m_HashCode = baseValue ^ Callback.GetHashCode();
					break;
				case DataType.Table:
					m_HashCode = baseValue ^ Table.GetHashCode();
					break;
				case DataType.Tuple:
				case DataType.TailCallRequest:
					m_HashCode = baseValue ^ Tuple.GetHashCode();
					break;
				case DataType.UserData:
				case DataType.Thread:
				default:
					m_HashCode = 999;
					break;
			}

			return m_HashCode;
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
		/// <returns>
		///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
		/// </returns>
		public override bool Equals(object obj)
		{
			DynValue other = obj as DynValue;

			if (other == null) return false;

			if ((other.Type == DataType.Nil && this.Type == DataType.Void)
				|| (other.Type == DataType.Void && this.Type == DataType.Nil))
				return true;

			if (other.Type != this.Type) return false;


			switch (Type)
			{
				case DataType.Void:
				case DataType.Nil:
					return true;
				case DataType.Boolean:
					return Boolean == other.Boolean;
				case DataType.Number:
					return Number == other.Number;
				case DataType.String:
					return String == other.String;
				case DataType.Function:
					return Function == other.Function;
				case DataType.ClrFunction:
					return Callback == other.Callback;
				case DataType.Table:
					return Table == other.Table;
				case DataType.Tuple:
				case DataType.TailCallRequest:
					return Tuple == other.Tuple;
				case DataType.Thread:
					return Coroutine == other.Coroutine;
				case DataType.UserData:
					{
						UserData ud1 = this.UserData;
						UserData ud2 = other.UserData;

						if (ud1 == null || ud2 == null)
							return false;

						if (ud1.Descriptor != ud2.Descriptor)
							return false;

						if (ud1.Object == null && ud2.Object == null)
							return true;

						if (ud1.Object != null && ud2.Object != null)
							return ud1.Object.Equals(ud2.Object);

						return false;
					}
				default:
					return object.ReferenceEquals(this, other);
			}
		}


		/// <summary>
		/// Casts this DynValue to string, using coercion if the type is number.
		/// </summary>
		/// <returns>The string representation, or null if not number, not string.</returns>
		public string CastToString()
		{
			DynValue rv = ToScalar();
			if (rv.Type == DataType.Number)
			{
				return rv.Number.ToString();
			}
			else if (rv.Type == DataType.String)
			{
				return rv.String;
			}
			return null;
		}

		/// <summary>
		/// Casts this DynValue to a double, using coercion if the type is string.
		/// </summary>
		/// <returns>The string representation, or null if not number, not string or non-convertible-string.</returns>
		public double? CastToNumber()
		{
			DynValue rv = ToScalar();
			if (rv.Type == DataType.Number)
			{
				return rv.Number;
			}
			else if (rv.Type == DataType.String)
			{
				double num;
				if (double.TryParse(rv.String, NumberStyles.Any, CultureInfo.InvariantCulture, out num))
					return num;
			}
			return null;
		}


		/// <summary>
		/// Casts this DynValue to a bool
		/// </summary>
		/// <returns>False if value is false or nil, true otherwise.</returns>
		public bool CastToBool()
		{
			DynValue rv = ToScalar();
			if (rv.Type == DataType.Boolean)
				return rv.Boolean;
			else return (rv.Type != DataType.Nil && rv.Type != DataType.Void);
		}

		/// <summary>
		/// Returns this DynValue as an instance of <see cref="IScriptPrivateResource"/>, if possible,
		/// null otherwise
		/// </summary>
		/// <returns>False if value is false or nil, true otherwise.</returns>
		public IScriptPrivateResource GetAsPrivateResource()
		{
			return m_Object as IScriptPrivateResource;
		}


		/// <summary>
		/// Converts a tuple to a scalar value. If it's already a scalar value, this function returns "this".
		/// </summary>
		public DynValue ToScalar()
		{
			if (Type != DataType.Tuple)
				return this;

			if (Tuple.Length == 0)
				return DynValue.Void;

			return Tuple[0].ToScalar();
		}

		/// <summary>
		/// Performs an assignment, overwriting the value with the specified one.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <exception cref="ScriptRuntimeException">If the value is readonly.</exception>
		public void Assign(DynValue value)
		{
			if (this.ReadOnly)
				throw new ScriptRuntimeException("Assigning on r-value");

			this.m_Number = value.m_Number;
			this.m_Object = value.m_Object;
			this.m_Type = value.Type;
			this.m_HashCode = -1;
		}

        /// <summary>
        /// Gets the length of a string or table value.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ScriptRuntimeException">Value is not a table or string.</exception>
        public DynValue GetLength()
		{
			if (this.Type == DataType.Table)
				return DynValue.NewNumber(this.Table.Length);
			if (this.Type == DataType.String)
				return DynValue.NewNumber(this.String.Length);

			throw new ScriptRuntimeException("Can't get length of type {0}", this.Type);
		}

		/// <summary>
		/// Determines whether this instance is nil or void
		/// </summary>
		public bool IsNil()
		{
			return this.Type == DataType.Nil || this.Type == DataType.Void;
		}

		/// <summary>
		/// Determines whether this instance is not nil or void
		/// </summary>
		public bool IsNotNil()
		{
			return this.Type != DataType.Nil && this.Type != DataType.Void;
		}

		/// <summary>
		/// Determines whether this instance is void
		/// </summary>
		public bool IsVoid()
		{
			return this.Type == DataType.Void;
		}

		/// <summary>
		/// Determines whether this instance is not void
		/// </summary>
		public bool IsNotVoid()
		{
			return this.Type != DataType.Void;
		}

		/// <summary>
		/// Determines whether is nil, void or NaN (and thus unsuitable for using as a table key).
		/// </summary>
		public bool IsNilOrNan()
		{
			return (this.Type == DataType.Nil) || (this.Type == DataType.Void) || (this.Type == DataType.Number && double.IsNaN(this.Number));
		}

		/// <summary>
		/// Changes the numeric value of a number DynValue.
		/// </summary>
		internal void AssignNumber(double num)
		{
			if (this.ReadOnly)
				throw new InternalErrorException(null, "Writing on r-value");

			if (this.Type != DataType.Number)
				throw new InternalErrorException("Can't assign number to type {0}", this.Type);

			this.m_Number = num;
		}

        /// <summary>
        /// Creates a new DynValue from a CLR genetic value
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public static DynValue FromObject<T>(Script script, T value)
        {
            return MoonSharp.Interpreter.Interop.Converters.ClrToScriptConversions.GenericToDynValue<T>(script, value);
        }

        /// <summary>
        /// Converts this MoonSharp DynValue to a CLR object.
        /// </summary>
        public T ToObject<T>()
		{
			return MoonSharp.Interpreter.Interop.Converters.ScriptToClrConversions.DynValueToObject<T>(this);
		}

		/// <summary>
		/// Converts this MoonSharp DynValue to a CLR object of the specified type.
		/// </summary>
		public object ToObject(Type desiredType)
		{
			//Contract.Requires(desiredType != null);
			return MoonSharp.Interpreter.Interop.Converters.ScriptToClrConversions.DynValueToObjectOfType(this, desiredType, null, false);
		}

#if HASDYNAMIC
		/// <summary>
		/// Converts this MoonSharp DynValue to a CLR object, marked as dynamic
		/// </summary>
		public dynamic ToDynamic()
		{
			return MoonSharp.Interpreter.Interop.Converters.ScriptToClrConversions.DynValueToObject(this);
		}
#endif

		/// <summary>
		/// Checks the type of this value corresponds to the desired type. A propert ScriptRuntimeException is thrown
		/// if the value is not of the specified type or - considering the TypeValidationFlags - is not convertible
		/// to the specified type.
		/// </summary>
		/// <param name="funcName">Name of the function requesting the value, for error message purposes.</param>
		/// <param name="desiredType">The desired data type.</param>
		/// <param name="argNum">The argument number, for error message purposes.</param>
		/// <param name="flags">The TypeValidationFlags.</param>
		/// <returns></returns>
		/// <exception cref="ScriptRuntimeException">Thrown
		/// if the value is not of the specified type or - considering the TypeValidationFlags - is not convertible
		/// to the specified type.</exception>
		public DynValue CheckType(string funcName, DataType desiredType, int argNum = -1, TypeValidationFlags flags = TypeValidationFlags.Default)
		{
			if (this.Type == desiredType)
				return this;

			bool allowNil = ((int)(flags & TypeValidationFlags.AllowNil) != 0);

			if (allowNil && this.IsNil())
				return this;

			bool autoConvert = ((int)(flags & TypeValidationFlags.AutoConvert) != 0);

			if (autoConvert)
			{
				if (desiredType == DataType.Boolean)
					return DynValue.NewBoolean(this.CastToBool());

				if (desiredType == DataType.Number)
				{
					double? v = this.CastToNumber();
					if (v.HasValue)
						return DynValue.NewNumber(v.Value);
				}

				if (desiredType == DataType.String)
				{
					string v = this.CastToString();
					if (v != null)
						return DynValue.NewString(v);
				}
			}

			if (this.IsVoid())
				throw ScriptRuntimeException.BadArgumentNoValue(argNum, funcName, desiredType);

			throw ScriptRuntimeException.BadArgument(argNum, funcName, desiredType, this.Type, allowNil);
		}

		/// <summary>
		/// Checks if the type is a specific userdata type, and returns it or throws.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="funcName">Name of the function.</param>
		/// <param name="argNum">The argument number.</param>
		/// <param name="flags">The flags.</param>
		/// <returns></returns>
		public T CheckUserDataType<T>(string funcName, int argNum = -1, TypeValidationFlags flags = TypeValidationFlags.Default)
		{
			DynValue v = this.CheckType(funcName, DataType.UserData, argNum, flags);
			bool allowNil = ((int)(flags & TypeValidationFlags.AllowNil) != 0);

			if (v.IsNil())
				return default(T);

		    T t;
		    if (v.UserData.TryGet<T>(out t))
		    {
		        return t;
		    }

			throw ScriptRuntimeException.BadArgumentUserData(argNum, funcName, typeof(T), t, allowNil);
		}

	}




}
