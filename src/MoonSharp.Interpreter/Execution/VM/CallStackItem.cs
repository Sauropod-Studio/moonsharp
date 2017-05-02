using MoonSharp.Interpreter.Debugging;

namespace MoonSharp.Interpreter.Execution.VM
{
	internal struct CallStackItem
	{
		public int Debug_EntryPoint;
		public SymbolRef[] Debug_Symbols;

		public SourceRef CallingSourceRef;

		public CallbackFunction ClrFunction;
		public CallbackFunction Continuation;
		public CallbackFunction ErrorHandler;
		public DynValue ErrorHandlerBeforeUnwind;

		public int BasePointer;
		public int ReturnAddress;
        public LocalScope LocalScope;
        public ClosureContext ClosureScope;

		public CallStackItemFlags Flags;

	    public void Release()
	    {
	        LocalScope.Release();
	    }
    }

    internal struct LocalScope
    {
        internal int[] _indices;

        public int Length
        {
            get { return _indices.Length; }
        }

        public LocalScope(int size)
        {
            _indices = PooledArray<int>.Request(size);
            Allocate();
        }

        public DynValue this[int key]
        {
            get
            {
                return HeapAllocatedDynValue.Get(_indices[key]);
            }
            set
            {
                HeapAllocatedDynValue.Set(_indices[key], ref value);
            }
        }

        public DynValue GetLocalDynValue(int i)
        {
            return HeapAllocatedDynValue.Get(_indices[i]);
        }
        public void SetLocalDynValue(int i, ref DynValue v)
        {
            HeapAllocatedDynValue.Set(_indices[i], ref v);
        }

        public int IndexAt(int i)
        {
            return _indices[i];
        }

        public void Allocate()
        {
            HeapAllocatedDynValue.Allocate(_indices);
        }

        public void Reassign(int i)
        {
            HeapAllocatedDynValue.DecreaseReferenceCount(_indices[i]);
            _indices[i] = HeapAllocatedDynValue.Allocate();
        }

        public void ReleaseValue(int i)
        {
            HeapAllocatedDynValue.DecreaseReferenceCount(_indices[i]);
        }

        public void Release()
        {
            HeapAllocatedDynValue.DecreaseReferenceCount(_indices);
            PooledArray<int>.Release(_indices);
        }

        public bool IsEmpty()
        {
            return _indices != null;
        }

    }
}
