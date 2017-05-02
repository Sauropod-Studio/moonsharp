using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Interop.BasicDescriptors;
using MoonSharp.Interpreter.Interop.Converters;
using MoonSharp.Interpreter.Interop.RegistrationPolicies;
using MoonSharp.Interpreter.Interop.StandardDescriptors;
using MoonSharp.Interpreter.Interop.UserDataRegistries;
using MoonSharp.Interpreter.Serialization.Json;

namespace MoonSharp.Interpreter
{

    public interface IUserData : IEquatable<IUserData>
    {
        T Get<T>();
        bool TryGet<T>(out T param);
        bool TrySet<T>(T param);
        DynValue UserValue { get; set; }
        IUserDataDescriptor Descriptor { get; set; }
        Type UnderlyingType { get;}
        bool HasValue();
        //bool Is(Type t);
        //bool Is<T>();
        string AsString();
    }

    public sealed class UserDataStruct<T> : RefIdObject, IUserData
    {
        public static int INSTANCE_AMOUNT;
        public const int MAX_POOL_SIZE = 10000;

        private static Stack<UserDataStruct<T>> _pool = new Stack<UserDataStruct<T>>(MAX_POOL_SIZE);

        public static UserDataStruct<T> Request()
        {
            UserDataStruct<T> ud;
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    ud = _pool.Pop();
                    GC.ReRegisterForFinalize(ud);
                }
                else
                {
                    ud = new UserDataStruct<T>();
                    INSTANCE_AMOUNT++;
                }
            }
            return ud;
        }

        ~UserDataStruct()
        {
            UserValue = DynValue.Invalid;
            t = default(T);
            Descriptor = null;
            if (_pool.Count < MAX_POOL_SIZE)
            {
                lock (_pool)
                {
                    _pool.Push(this);
                }
            }
        }

        private T t;

        public IUserDataDescriptor Descriptor { get; set; }

        public DynValue UserValue { get; set; }

        public Type UnderlyingType { get { return typeof(T); } }

        public string AsString() { return Descriptor.AsString(this); }

        public bool Is(Type type) { return typeof(T).IsAssignableFrom(type); }

        public bool Is<TYPE>() { return t is TYPE; }

        public TParam Get<TParam>()
        {
            return ValueConverter<T, TParam>.Instance.Convert(t);
        }

        public bool TryGet<TParam>(out TParam param)
        {
            bool success = false;
            if (typeof(T) == typeof(TParam))
            {
                param = ValueConverter<T, TParam>.Instance.Convert(t);
                success = true;
            }
            else
            {
                param = default(TParam);
            }
            return success;
        }

        public bool TrySet<TParam>(TParam param)
        {
            bool success = false;
            if (typeof(TParam) == typeof(T))
            {
                t = ValueConverter<TParam, T>.Instance.Convert(param);
                success = true;
            }
            return success;
        }

        public bool HasValue() { return true; }

        public bool Equals(IUserData other)
        {
            T otherT;
            if (other.TryGet(out otherT))
                return t.Equals(otherT);
            return false;
        }
    }



    public sealed class UserDataRef : RefIdObject, IUserData
    {
        public static int INSTANCE_AMOUNT;
        public const int MAX_POOL_SIZE = 10000;

        private static Stack<UserDataRef> _pool = new Stack<UserDataRef>(MAX_POOL_SIZE);

        public static UserDataRef Request()
        {
            UserDataRef ud;
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    ud = _pool.Pop();
                    GC.ReRegisterForFinalize(ud);
                }
                else
                {
                    ud = new UserDataRef();
                    INSTANCE_AMOUNT++;
                }
            }
            return ud;
        }

        ~UserDataRef()
        {
            UserValue = DynValue.Invalid;
            _object = null;
            Descriptor = null;
            lock (_pool)
            {
                if (_pool.Count < MAX_POOL_SIZE)
                    _pool.Push(this);
            }
        }

        private object _object;

        public IUserDataDescriptor Descriptor { get; set; }

        public DynValue UserValue { get; set; }

        public Type UnderlyingType { get { return _object.GetType(); } }

        public bool HasValue() { return _object != null; }
        public string AsString() { return Descriptor.AsString(this); }

        public TParam Get<TParam>()
        {
            return (TParam)_object;
        }

        public bool TryGet<TParam>(out TParam param)
        {
            bool success = false;
            if (_object is TParam)
            {
                param = (TParam)_object;
                success = true;
            }
            else
            {
                param = default(TParam);
            }
            return success;
        }

        public bool TrySet<TParam>(TParam param)
        {
            _object = param;
            return true;
        }

        public bool Equals(IUserData other)
        {
            object otherT;
            if (other.TryGet(out otherT))
                return _object.Equals(otherT);
            return false;
        }

        internal static void WarnDynValueCache()
        {
            lock (_pool)
            {
                INSTANCE_AMOUNT = MAX_POOL_SIZE;
                for (int i = MAX_POOL_SIZE; i > 0; i--)
                    _pool.Push(new UserDataRef());
            }
        }
    }
    /// <summary>
    /// Class exposing C# objects as Lua userdata.
    /// For efficiency, a global registry of types is maintained, instead of a per-script one.
    /// </summary>
    public static class UserData
	{
        static UserData()
        {
            RegistrationPolicy = InteropRegistrationPolicy.Default;

            RegisterType<EventFacade>(InteropAccessMode.NoReflectionAllowed);
            RegisterType<AnonWrapper>(InteropAccessMode.HideMembers);
            RegisterType<EnumerableWrapper>(InteropAccessMode.NoReflectionAllowed);
            RegisterType<JsonNull>(InteropAccessMode.Reflection);

            DefaultAccessMode = InteropAccessMode.LazyOptimized;

            new System.Threading.Thread(() => UserDataRef.WarnDynValueCache()).Start();
        }

        /// <summary>
        /// Registers a type for userdata interop
        /// </summary>
        /// <typeparam name="T">The type to be registered</typeparam>
        /// <param name="accessMode">The access mode (optional).</param>
        /// <param name="friendlyName">Friendly name for the type (optional)</param>
        public static IUserDataDescriptor RegisterType<T>(InteropAccessMode accessMode = InteropAccessMode.Default, string friendlyName = null)
		{
			return TypeDescriptorRegistry.RegisterType_Impl(typeof(T), accessMode, friendlyName, null);
		}

		/// <summary>
		/// Registers a type for userdata interop
		/// </summary>
		/// <param name="type">The type to be registered</param>
		/// <param name="accessMode">The access mode (optional).</param>
		/// <param name="friendlyName">Friendly name for the type (optional)</param>
		public static IUserDataDescriptor RegisterType(Type type, InteropAccessMode accessMode = InteropAccessMode.Default, string friendlyName = null)
		{
			return TypeDescriptorRegistry.RegisterType_Impl(type, accessMode, friendlyName, null);
		}


		/// <summary>
		/// Registers a proxy type.
		/// </summary>
		/// <param name="proxyFactory">The proxy factory.</param>
		/// <param name="accessMode">The access mode.</param>
		/// <param name="friendlyName">A friendly name for the descriptor.</param>
		/// <returns></returns>
		public static IUserDataDescriptor RegisterProxyType(IProxyFactory proxyFactory, InteropAccessMode accessMode = InteropAccessMode.Default, string friendlyName = null)
		{
			return TypeDescriptorRegistry.RegisterProxyType_Impl(proxyFactory, accessMode, friendlyName);
		}

		/// <summary>
		/// Registers a proxy type using a delegate.
		/// </summary>
		/// <typeparam name="TProxy">The type of the proxy.</typeparam>
		/// <typeparam name="TTarget">The type of the target.</typeparam>
		/// <param name="wrapDelegate">A delegate creating a proxy object from a target object.</param>
		/// <param name="accessMode">The access mode.</param>
		/// <param name="friendlyName">A friendly name for the descriptor.</param>
		/// <returns></returns>
		public static IUserDataDescriptor RegisterProxyType<TProxy, TTarget>(Func<TTarget, TProxy> wrapDelegate, InteropAccessMode accessMode = InteropAccessMode.Default, string friendlyName = null)
			where TProxy : class
			where TTarget : class
		{
			return RegisterProxyType(new DelegateProxyFactory<TProxy, TTarget>(wrapDelegate), accessMode, friendlyName);
		}



		/// <summary>
		/// Registers a type with a custom userdata descriptor
		/// </summary>
		/// <typeparam name="T">The type to be registered</typeparam>
		/// <param name="customDescriptor">The custom descriptor.</param>
		public static IUserDataDescriptor RegisterType<T>(IUserDataDescriptor customDescriptor)
		{
			return TypeDescriptorRegistry.RegisterType_Impl(typeof(T), InteropAccessMode.Default, null, customDescriptor);
		}

		/// <summary>
		/// Registers a type with a custom userdata descriptor
		/// </summary>
		/// <param name="type">The type to be registered</param>
		/// <param name="customDescriptor">The custom descriptor.</param>
		public static IUserDataDescriptor RegisterType(Type type, IUserDataDescriptor customDescriptor)
		{
			return TypeDescriptorRegistry.RegisterType_Impl(type, InteropAccessMode.Default, null, customDescriptor);
		}

		/// <summary>
		/// Registers a type with a custom userdata descriptor
		/// </summary>
		/// <param name="customDescriptor">The custom descriptor.</param>
		public static IUserDataDescriptor RegisterType(IUserDataDescriptor customDescriptor)
		{
			return TypeDescriptorRegistry.RegisterType_Impl(customDescriptor.Type, InteropAccessMode.Default, null, customDescriptor);
		}


		/// <summary>
		/// Registers all types marked with a MoonSharpUserDataAttribute that ar contained in an assembly.
		/// </summary>
		/// <param name="asm">The assembly.</param>
		/// <param name="includeExtensionTypes">if set to <c>true</c> extension types are registered to the appropriate registry.</param>
		public static void RegisterAssembly(Assembly asm = null, bool includeExtensionTypes = false)
		{
			if (asm == null)
			{
				#if NETFX_CORE || DOTNET_CORE
					throw new NotSupportedException("Assembly.GetCallingAssembly is not supported on target framework.");
				#else
					asm = Assembly.GetCallingAssembly();
				#endif
			}

			TypeDescriptorRegistry.RegisterAssembly(asm, includeExtensionTypes);
		}

		/// <summary>
		/// Determines whether the specified type is registered. Note that this should be used only to check if a descriptor
		/// has been registered EXACTLY. For many types a descriptor can still be created, for example through the descriptor
		/// of a base type or implemented interfaces.
		/// </summary>
		/// <param name="t">The type.</param>
		/// <returns></returns>
		public static bool IsTypeRegistered(Type t)
		{
			return TypeDescriptorRegistry.IsTypeRegistered(t);
		}

		/// <summary>
		/// Determines whether the specified type is registered. Note that this should be used only to check if a descriptor
		/// has been registered EXACTLY. For many types a descriptor can still be created, for example through the descriptor
		/// of a base type or implemented interfaces.
		/// </summary>
		/// <typeparam name="T">The type.</typeparam>
		/// <returns></returns>
		public static bool IsTypeRegistered<T>()
		{
			return TypeDescriptorRegistry.IsTypeRegistered(typeof(T));
		}

		/// <summary>
		/// Unregisters a type. 
		/// WARNING: unregistering types at runtime is a dangerous practice and may cause unwanted errors.
		/// Use this only for testing purposes or to re-register the same type in a slightly different way.
		/// Additionally, it's a good practice to discard all previous loaded scripts after calling this method.
		/// </summary>
		/// <typeparam name="T">The type to be unregistered</typeparam>
		public static void UnregisterType<T>()
		{
			TypeDescriptorRegistry.UnregisterType(typeof(T));
		}

		/// <summary>
		/// Unregisters a type.
		/// WARNING: unregistering types at runtime is a dangerous practice and may cause unwanted errors.
		/// Use this only for testing purposes or to re-register the same type in a slightly different way.
		/// Additionally, it's a good practice to discard all previous loaded scripts after calling this method.
		/// </summary>
		/// <param name="t">The The type to be unregistered</param>
		public static void UnregisterType(Type t)
		{
			TypeDescriptorRegistry.UnregisterType(t);
		}

		/// <summary>
		/// Creates a userdata DynValue from the specified object, using a specific descriptor
		/// </summary>
		/// <param name="o">The object</param>
		/// <param name="descr">The descriptor.</param>
		/// <returns></returns>
		public static DynValue CreateWithDescriptor<T>(T o, IUserDataDescriptor descr)
		{
		    IUserData userData;

		    if (typeof (T).IsValueType)
		    {
		        userData = UserDataStruct<T>.Request();
		    }
		    else
		    {
                userData = UserDataRef.Request();
            }
            userData.Descriptor = descr;
            userData.TrySet(o);

            return DynValue.NewUserData(userData);
		}

		/// <summary>
		/// Creates a userdata DynValue from the specified object
		/// </summary>
		/// <param name="o">The object</param>
		/// <returns></returns>
		public static DynValue Create<T>(T o)
		{
			var descr = GetDescriptorForObject(o);
			if (descr == null)
			{
                if (o is Type)
					return CreateStatic(ValueConverter<T, Type>.Instance.Convert(o));
				return DynValue.Invalid;
			}

			return CreateWithDescriptor(o, descr);
		}

		/// <summary>
		/// Creates a static userdata DynValue from the specified IUserDataDescriptor
		/// </summary>
		/// <param name="descr">The IUserDataDescriptor</param>
		/// <returns></returns>
		public static DynValue CreateStatic(IUserDataDescriptor descr)
		{
		    if (descr == null)
                return DynValue.Invalid;

		    var userData = UserDataRef.Request();
		    userData.Descriptor = descr;
		    return DynValue.NewUserData(userData);
		}

        /// <summary>
        /// Creates a static userdata DynValue from the specified Type
        /// </summary>
        /// <param name="t">The type</param>
        /// <returns></returns>
        public static DynValue CreateStatic(Type t)
        {
            return CreateStatic(GetDescriptorForType(t, false));
		}

		/// <summary>
		/// Creates a static userdata DynValue from the specified Type
		/// </summary>
		/// <typeparam name="T">The Type</typeparam>
		/// <returns></returns>
		public static DynValue CreateStatic<T>()
		{
			return CreateStatic(GetDescriptorForType(typeof(T), false));
		}

		/// <summary>
		/// Gets or sets the registration policy to be used in the whole application
		/// </summary>
		public static IRegistrationPolicy RegistrationPolicy
		{
			get { return TypeDescriptorRegistry.RegistrationPolicy; }
			set { TypeDescriptorRegistry.RegistrationPolicy = value; }
		}

		/// <summary>
		/// Gets or sets the default access mode to be used in the whole application
		/// </summary>
		/// <value>
		/// The default access mode.
		/// </value>
		/// <exception cref="System.ArgumentException">InteropAccessMode is InteropAccessMode.Default</exception>
		public static InteropAccessMode DefaultAccessMode
		{
			get { return TypeDescriptorRegistry.DefaultAccessMode; }
			set { TypeDescriptorRegistry.DefaultAccessMode = value; }
		}

		/// <summary>
		/// Registers an extension Type (that is a type containing extension methods)
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="mode">The InteropAccessMode.</param>
		public static void RegisterExtensionType(Type type, InteropAccessMode mode = InteropAccessMode.Default)
		{
			ExtensionMethodsRegistry.RegisterExtensionType(type, mode);
		}

		/// <summary>
		/// Gets all the extension methods which can match a given name and extending a given Type
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="extendedType">The extended type.</param>
		/// <returns></returns>
		public static List<IOverloadableMemberDescriptor> GetExtensionMethodsByNameAndType(string name, Type extendedType)
		{
			return ExtensionMethodsRegistry.GetExtensionMethodsByNameAndType(name, extendedType);
		}

		/// <summary>
		/// Gets a number which gets incremented everytime the extension methods registry changes.
		/// Use this to invalidate caches based on extension methods
		/// </summary>
		/// <returns></returns>
		public static int GetExtensionMethodsChangeVersion()
		{
			return ExtensionMethodsRegistry.GetExtensionMethodsChangeVersion();
		}

		/// <summary>
		/// Gets the best possible type descriptor for a specified CLR type.
		/// </summary>
		/// <typeparam name="T">The CLR type for which the descriptor is desired.</typeparam>
		/// <param name="searchInterfaces">if set to <c>true</c> interfaces are used in the search.</param>
		/// <returns></returns>
		public static IUserDataDescriptor GetDescriptorForType<T>(bool searchInterfaces)
		{
			return TypeDescriptorRegistry.GetDescriptorForType(typeof(T), searchInterfaces);
		}

		/// <summary>
		/// Gets the best possible type descriptor for a specified CLR type.
		/// </summary>
		/// <param name="type">The CLR type for which the descriptor is desired.</param>
		/// <param name="searchInterfaces">if set to <c>true</c> interfaces are used in the search.</param>
		/// <returns></returns>
		public static IUserDataDescriptor GetDescriptorForType(Type type, bool searchInterfaces)
		{
			return TypeDescriptorRegistry.GetDescriptorForType(type, searchInterfaces);
		}


		/// <summary>
		/// Gets the best possible type descriptor for a specified CLR object.
		/// </summary>
		/// <param name="o">The object.</param>
		/// <returns></returns>
		public static IUserDataDescriptor GetDescriptorForObject<T>(T t)
		{
			return TypeDescriptorRegistry.GetDescriptorForType(typeof(T).IsValueType ? typeof(T) : t.GetType(), true);
		}


		/// <summary>
		/// Gets a table with the description of registered types.
		/// </summary>
		/// <param name="useHistoricalData">if set to true, it will also include the last found descriptor of all unregistered types.</param>
		/// <returns></returns>
		public static Table GetDescriptionOfRegisteredTypes(bool useHistoricalData = false)
		{
			DynValue output = DynValue.NewPrimeTable();
			var registeredTypesPairs = useHistoricalData ? TypeDescriptorRegistry.RegisteredTypesHistory : TypeDescriptorRegistry.RegisteredTypes;

			foreach (var descpair in registeredTypesPairs)
			{
				IWireableDescriptor sd = descpair.Value as IWireableDescriptor;

				if (sd != null)
				{
					DynValue t = DynValue.NewPrimeTable();
					output.Table.Set(descpair.Key.FullName, t);
					sd.PrepareForWiring(t.Table);
				}
			}

			return output.Table;
		}

		/// <summary>
		/// Gets all the registered types.
		/// </summary>
		/// <param name="useHistoricalData">if set to true, it will also include the last found descriptor of all unregistered types.</param>
		/// <returns></returns>
		public static IEnumerable<Type> GetRegisteredTypes(bool useHistoricalData = false)
		{
			var registeredTypesPairs = useHistoricalData ? TypeDescriptorRegistry.RegisteredTypesHistory : TypeDescriptorRegistry.RegisteredTypes;
			return registeredTypesPairs.Select(p => p.Value.Type);
		}

		

	}
}
