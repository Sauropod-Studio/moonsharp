using System.Collections.Generic;
using MoonSharp.Interpreter.Execution;
using System;

namespace MoonSharp.Interpreter
{
	/// <summary>
	/// A class representing a script function
	/// </summary>
	public class Closure : RefIdObject, IScriptPrivateResource
	{
		/// <summary>
		/// Type of closure based on upvalues
		/// </summary>
		public enum UpvaluesType
		{
			/// <summary>
			/// The closure has no upvalues (thus, technically, it's a function and not a closure!)
			/// </summary>
			None,
			/// <summary>
			/// The closure has _ENV as its only upvalue
			/// </summary>
			Environment,
			/// <summary>
			/// The closure is a "real" closure, with multiple upvalues
			/// </summary>
			Closure
		}


		/// <summary>
		/// Gets the entry point location in bytecode .
		/// </summary>
		public int EntryPointByteCodeLocation { get; private set; }

        public bool isAlive { get; private set; }


		/// <summary>
		/// Gets the script owning this function
		/// </summary>
		public Script OwnerScript { get; private set; }


		/// <summary>
		/// Shortcut for an empty closure
		/// </summary>
		private static ClosureContext emptyClosure = new ClosureContext(new ClosureRefValue[0]);

		/// <summary>
		/// The current closure context
		/// </summary>
		internal ClosureContext ClosureContext { get; private set; }

        public static int count = 0;
        public static int countGC = 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="Closure"/> class.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="idx">The index.</param>
		/// <param name="resolvedLocals">The resolved locals.</param>
		internal Closure(Script script, int idx, ClosureRefValue[] resolvedLocals)
        {
            count++;
            countGC++;
            isAlive = true;
            OwnerScript = script;
			EntryPointByteCodeLocation = idx;
			ClosureContext = new ClosureContext(resolvedLocals);
            script.RegisterClosure(this);
        }

        public static void Kill(ref Closure tokill)
        {
            _Kill(tokill);
            tokill = null;
        }

        private static void _Kill(Closure tokill)
        {
            if (tokill != null && tokill.isAlive)
            {
                count--;
                tokill.ClosureContext.ReleaseValues();
                tokill.ClosureContext = emptyClosure;
                tokill.OwnerScript = null;
                tokill.isAlive = false;
            }
        }

	    ~Closure()
	    {
            _Kill(this);
            countGC--;
        }

		/// <summary>
		/// Calls this function no args, doesn't allocate an IList
		/// </summary>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call()
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this));
		}

        /// <summary>
        /// Calls this function no args, doesn't allocate an IList
        /// </summary>
        /// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue arg1)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), arg1);
        }

        /// <summary>
		/// Calls this function no args, doesn't allocate an IList
		/// </summary>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue arg1, DynValue arg2)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), arg1, arg2);
        }

        /// <summary>
		/// Calls this function no args, doesn't allocate an IList
		/// </summary>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue arg1, DynValue arg2, DynValue arg3)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), arg1, arg2, arg3);
        }

        /// <summary>
		/// Calls this function no args, doesn't allocate an IList
		/// </summary>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(DynValue arg1, DynValue arg2, DynValue arg3, DynValue arg4)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), arg1, arg2, arg3, arg4);
        }

        /// <summary>
        /// Calls this function with the specified args
        /// </summary>
        /// <param name="args">The arguments to pass to the function.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
        public DynValue Call(params object[] args)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), args);
		}

		/// <summary>
		/// Calls this function with the specified args
		/// </summary>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(params DynValue[] args)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), args);
		}

        /// <summary>
		/// Calls this function with the specified args
		/// </summary>
		/// <param name="args">The arguments (count > 0) to pass to the function. (Argument 0 MUST be empty!)</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(IList<DynValue> args)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to Call on dead Closure"));
            return OwnerScript.Call(DynValue.NewClosure(this), args);
        }


        /// <summary>
        /// Gets a delegate wrapping calls to this scripted function
        /// </summary>
        /// <returns></returns>
        public ScriptFunctionDelegate GetDelegate()
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetDelegate on dead Closure"));
            return args => this.Call(args).ToObject<object>();
		}

		/// <summary>
		/// Gets a delegate wrapping calls to this scripted function
		/// </summary>
		/// <typeparam name="T">The type of return value of the delegate.</typeparam>
		/// <returns></returns>
		public ScriptFunctionDelegate<T> GetDelegate<T>()
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetDelegate on dead Closure"));
            return args => this.Call(args).ToObject<T>();
		}

		/// <summary>
		/// Gets the number of upvalues in this closure
		/// </summary>
		/// <returns>The number of upvalues in this closure</returns>
		public int GetUpvaluesCount()
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetUpvaluesCount on dead Closure"));
            return ClosureContext.Count;
		}

		/// <summary>
		/// Gets the name of the specified upvalue.
		/// </summary>
		/// <param name="idx">The index of the upvalue.</param>
		/// <returns>The upvalue name</returns>
		public string GetUpvalueName(int idx)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetUpvalueName on dead Closure"));
            return ClosureContext[idx].Symbol.Name;
		}

		/// <summary>
		/// Gets the value of an upvalue. To set the value, use GetUpvalue(idx).Assign(...);
		/// </summary>
		/// <param name="idx">The index of the upvalue.</param>
		/// <returns>The value of an upvalue </returns>
		public DynValue GetUpvalue(int idx)
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetUpvalue on dead Closure"));
            return ClosureContext[idx].Get();
		}

		/// <summary>
		/// Gets the type of the upvalues contained in this closure
		/// </summary>
		/// <returns></returns>
		public UpvaluesType GetUpvaluesType()
        {
            if (!isAlive) throw new InvalidOperationException(string.Format("Attempting to GetUpvaluesType on dead Closure"));
            int count = GetUpvaluesCount();

			if (count == 0)
				return UpvaluesType.None;
			else if (count == 1 && GetUpvalueName(0) == WellKnownSymbols.ENV)
				return UpvaluesType.Environment;
			else
				return UpvaluesType.Closure;
		}


	}
}
