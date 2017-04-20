using System.Collections.Generic;
using System.Linq;

namespace MoonSharp.Interpreter.Execution
{
	/// <summary>
	/// The scope of a closure (container of upvalues)
	/// </summary>
	internal struct ClosureContext 
	{
        private static long ID_GEN = 1;

        /// <summary>
        /// Gets a value indicating whether this location is inside CLR .
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// Gets the symbols.
        /// </summary>
        public SymbolsWrapper Symbols { get; private set; }

        /// <summary>
        /// Gets the dynValues.
        /// </summary>
        public DynValue[] Values { get; }

        internal ClosureContext(SymbolRef[] symbols, DynValue[] values)
        {
            Id = System.Threading.Interlocked.Increment(ref ID_GEN);
            Symbols = new SymbolsWrapper(symbols);
            Values = values;
		}

        public bool IsEmpty() { return Id != 0; }

        public DynValue this[int i]
	    {
	        get { return Values[i]; }
            set { Values[i] = value; }
        }

        public int Count { get { return Values.Length;} }

	    public void ReleaseArray()
	    {
            DynValueArray.Release(Values);
        }
    }

    internal struct SymbolsWrapper
    {
        private SymbolRef[] _symbols;

        public SymbolsWrapper(SymbolRef[] symbols)
        {
            this._symbols = symbols;
        }

        public string this[int i] { get { return _symbols[i].Name; } }

        public int Length { get { return _symbols.Length; } }
    }
}
