using System.Collections.Generic;
using System.Linq;

namespace MoonSharp.Interpreter.Execution
{
	/// <summary>
	/// The scope of a closure (container of upvalues)
	/// </summary>
	internal struct ClosureContext
	{
	    private ClosureRefValue[] _closureRefs;

        internal ClosureContext(ClosureRefValue[] refs)
        {
            _closureRefs = refs;
            for (int index = 0; index < _closureRefs.Length; index++)
            {
                _closureRefs[index].IncrementReferenceCount();
            }
        }

	    public bool IsEmpty() { return _closureRefs == null; }

        public ClosureRefValue this[int i]
	    {
	        get { return _closureRefs[i]; }
        }

        public int Count { get { return _closureRefs.Length;} }

	    public void ReplaceWith(int i, ClosureRefValue refValue)
	    {
	        _closureRefs[i].DecreaseReferenceCount();
            _closureRefs[i] = refValue;
            refValue.IncrementReferenceCount();
        }

	    public void ReleaseValues()
	    {
            for (int index = 0; index < _closureRefs.Length; index++)
            {
                _closureRefs[index].DecreaseReferenceCount();
            }
        }
    }

    internal struct ClosureRefValue
    {
        private int index;
        public SymbolRef Symbol;

        public ClosureRefValue(SymbolRef s, int index)
        {
            this.index = index;
            Symbol = s;
        }

        public DynValue Get()
        {
            return HeapAllocatedDynValue.Get(index);
        }

        public void Set(ref DynValue v)
        {
            HeapAllocatedDynValue.Set(index, ref v);
        }

        public void DecreaseReferenceCount()
        {
            HeapAllocatedDynValue.DecreaseReferenceCount(index);
        }

        public void IncrementReferenceCount()
        {
            HeapAllocatedDynValue.IncrementReferenceCount(index);
        }
    }
}
