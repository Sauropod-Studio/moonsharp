using System;

namespace MoonSharp.Interpreter.Interop
{
    public struct LuaEnumProxy<T> where T : struct
    {
        public T value;
        public LuaEnumProxy(T value)
        {
            this.value = value;
        }

        static public implicit operator T(LuaEnumProxy<T> proxy)
        {
            return proxy.value;
        }

        static public implicit operator LuaEnumProxy<T>(T enu)
        {
            return new LuaEnumProxy<T>(enu);
        }
    }
    
    public class LuaEnumDescriptor<T> : IUserDataDescriptor where T : struct
    {

        public string Name 
        {
            get { return Type.FullName; }
        }

		public Type Type 
        {
            get { return typeof(T); }
        }

        public Table CreateTable()
        {
            Table t = new Table(null);
            string[] keys = Enum.GetNames(Type);
            Array values = Enum.GetValues(Type);
            for (int i = 0; i != values.Length; i++)
            {
                t[keys[i]] = UserData.Create(new LuaEnumProxy<T>((T)values.GetValue(i)), this);
            }
            return t;
        }

        public DynValue CreateDynValue()
        {
            return DynValue.NewTable(CreateTable());
        }

        public LuaEnumDescriptor()
		{
            UserData.RegisterType<LuaEnumProxy<T>>(this);
        }

		public DynValue Index(Script script, IUserData obj, DynValue index, bool isDirectIndexing)
		{
            if (index.String == "int")
            {
                return DynValue.NewNumber((int)(object)obj.Get<LuaEnumProxy<T>>().value);
            }
            if (index.String == "str")
            {
                return DynValue.NewString(Enum.GetName(typeof(T), obj.Get<LuaEnumProxy<T>>().value));
            }
            return null;
		}

		public bool SetIndex(Script script, IUserData obj, DynValue index, DynValue value, bool isDirectIndexing)
		{
            return false;
		}

		public string AsString(IUserData obj)
		{
			return (obj != null) ? Enum.GetName(typeof(T), obj.Get<LuaEnumProxy<T>>().value) : null;
		}

        /// <summary>
        /// 
        /// Gets a "meta" operation on this userdata. If a descriptor does not support this functionality,
        /// it should return "null" (not a nil). 
        /// 
        /// These standard metamethods can be supported (the return value should be a function accepting the
        /// classic parameters of the corresponding metamethod):
        /// __add, __sub, __mul, __div, __div, __pow, __unm, __eq, __lt, __le, __lt, __len, __concat, 
        /// __pairs, __ipairs, __iterator, __call
        /// 
        /// These standard metamethods are supported through other calls for efficiency:
        /// __index, __newindex, __tostring
        /// 
        /// </summary>
        /// <param name="script">The script originating the request</param>
        /// <param name="obj">The object (null if a static request is done)</param>
        /// <param name="metaname">The name of the metamember.</param>
        /// <returns></returns>
        public DynValue MetaIndex(Script script, IUserData obj, string metaname)
        {
            if (metaname == "__eq")
            {
                return __eq;
            }

            return null;
        }

        public bool IsTypeCompatible(Type type, IUserData obj)
        {
            return type.IsInstanceOfType(obj);
        }

        public static DynValue __eq = DynValue.NewCallback((con, args) => ___eq(con, args));
        private static DynValue ___eq(ScriptExecutionContext context, CallbackArguments args)
        {
            T lhs;
            DynValue rhs;
            {
                //Raw get and nullchecks
                DynValue r_lhs = args.RawGet(0, true);
                DynValue r_rhs = args.RawGet(1, true);

                if (r_lhs == null || r_lhs.IsNil() || r_rhs == null || r_rhs.IsNil()) return DynValue.False;

                //Make sure lhs is a proper T
                {
                    LuaEnumProxy<T> holder;
                    if (r_lhs.Type == DataType.UserData && r_lhs.UserData.TryGet(out holder)) { rhs = r_rhs; }
                    if (r_rhs.Type == DataType.UserData && r_rhs.UserData.TryGet(out holder)) { rhs = r_lhs; }
                    else { return DynValue.False; }
                    lhs = holder.value;
                }
            }

            //Test T against rhs
            if (rhs.Type == DataType.Number)
            {
                return DynValue.NewBoolean(lhs.Equals(Enum.ToObject(typeof(T), (int)rhs.Number)));
            }
            if (rhs.Type == DataType.String)
            {
                return DynValue.NewBoolean(lhs.Equals(Enum.Parse(typeof(T), rhs.String)));
            }
            if (rhs.Type == DataType.UserData)
            {
                LuaEnumProxy<T> t2;
                if (rhs.UserData.TryGet(out t2))
                {
                    return DynValue.NewBoolean(lhs.Equals(t2.value));
                }
            }
            return DynValue.False;
        }

    }
}
