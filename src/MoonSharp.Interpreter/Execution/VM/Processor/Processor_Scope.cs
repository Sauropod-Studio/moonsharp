using System;
using System.Runtime.Remoting.Messaging;

namespace MoonSharp.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		private void ClearBlockData(Instruction I)
		{
			int from = I.NumVal;
			int to = I.NumVal2;

			var callStackItem = this.m_ExecutionStack.Peek();

            if (to >= 0 && from >= 0 && to >= from)
            {
                for (int i = from; i <= to; i++)
                    callStackItem.LocalScope.Reassign(i);
            }
        }


		public DynValue GetGenericSymbol(SymbolRef symref)
		{
            switch (symref.i_Type)
			{
				case  SymbolRefType.DefaultEnv:
					return DynValue.NewTable(this.GetScript().Globals);
				case SymbolRefType.Global:
					return GetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name);
				case SymbolRefType.Local:
                    CallStackItem stackframe;
                    GetTopNonClrFunction(out stackframe);
                    return stackframe.LocalScope[symref.i_Index];
				case SymbolRefType.Upvalue:
                    GetTopNonClrFunction(out stackframe);
                    return stackframe.ClosureScope[symref.i_Index].Get();
				default:
					throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type, symref.i_Name);
			}
		}

		private DynValue GetGlobalSymbol(DynValue dynValue, string name)
		{
			if (dynValue.Type != DataType.Table)
				throw new InvalidOperationException(string.Format("_ENV is not a table but a {0}", dynValue.Type));

			return dynValue.Table.Get(name);
		}

		private void SetGlobalSymbol(DynValue dynValue, string name, DynValue value)
		{
			if (dynValue.Type != DataType.Table)
				throw new InvalidOperationException(string.Format("_ENV is not a table but a {0}", dynValue.Type));

			dynValue.Table.Set(name, value.IsValid ? value : DynValue.Nil);
		}


		public void AssignGenericSymbol(SymbolRef symref, DynValue value)
		{
			switch (symref.i_Type)
			{
				case SymbolRefType.Global:
					SetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name, value);
					break;
				case SymbolRefType.Local:
					{
						CallStackItem stackframe;
                        GetTopNonClrFunction(out stackframe);
						stackframe.LocalScope[symref.i_Index] = value;
					}
					break;
				case SymbolRefType.Upvalue:
					{
                        CallStackItem stackframe;
                        GetTopNonClrFunction(out stackframe);

					    stackframe.ClosureScope[symref.i_Index].Set(ref value);
					}
					break;
				case SymbolRefType.DefaultEnv:
					{
						throw new ArgumentException("Can't AssignGenericSymbol on a DefaultEnv symbol");
					}
				default:
					throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type, symref.i_Name);
			}
		}

		bool GetTopNonClrFunction(out CallStackItem csi)
		{
			csi = default(CallStackItem);

			for (int i = 0; i < m_ExecutionStack.Count; i++)
			{
                csi = m_ExecutionStack.Peek(i);

				if (csi.ClrFunction == null)
					return true;
			}

			return false;
		}


		public SymbolRef FindSymbolByName(string name)
		{
			if (m_ExecutionStack.Count > 0)
			{
			    CallStackItem stackframe;

				if (GetTopNonClrFunction(out stackframe))
				{
					if (stackframe.Debug_Symbols != null)
					{
						for (int i = stackframe.Debug_Symbols.Length - 1; i >= 0; i--)
						{
							var l = stackframe.Debug_Symbols[i];

							if (l.i_Name == name && stackframe.LocalScope[i].IsValid)
								return l;
						}
					}


					var closure = stackframe.ClosureScope;

					if (!closure.IsEmpty())
					{
						for (int i = 0; i < closure.Count; i++)
							if (closure[i].Symbol.Name == name)
								return SymbolRef.Upvalue(name, i);
					}
				}
			}

			if (name != WellKnownSymbols.ENV)
			{
				SymbolRef env = FindSymbolByName(WellKnownSymbols.ENV);
				return SymbolRef.Global(name, env);
			}
			else
			{
				return SymbolRef.DefaultEnv;
			}
		}

	}
}
