using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MoonSharp.Interpreter.Interop.Converters
{
	internal static class ClrToScriptConversions
	{
		/// <summary>
		/// Tries to convert a CLR object to a MoonSharp value, using "trivial" logic.
		/// Skips on custom conversions, etc.
		/// Does NOT throw on failure.
		/// </summary>
		internal static DynValue TryObjectToTrivialDynValue(Script script, object obj)
		{
			if (obj == null)
				return DynValue.Nil;

			if (obj is DynValue)
				return (DynValue)obj;

			Type t = obj.GetType();

			if (obj is bool)
				return DynValue.NewBoolean((bool)obj);

			if (obj is string || obj is StringBuilder || obj is char)
				return DynValue.NewString(obj.ToString());

			if (NumericConversions.NumericTypes.Contains(t))
				return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

			if (obj is Table)
				return DynValue.NewTable((Table)obj);

			return null;
		}

		/// <summary>
		/// Tries to convert a CLR object to a MoonSharp value, using "simple" logic.
		/// Does NOT throw on failure.
		/// </summary>
		internal static DynValue TryObjectToSimpleDynValue(Script script, object obj)
		{
			if (obj == null)
				return DynValue.Nil;

			if (obj is DynValue)
				return (DynValue)obj;


			var converter = Script.GlobalOptions.CustomConverters.GetClrToScriptCustomConversion(obj.GetType());
			if (converter != null)
			{
				var v = converter(script, obj);
				if (v != null)
					return v;
			}

			Type t = obj.GetType();

			if (obj is string || obj is StringBuilder || obj is char)
				return DynValue.NewString(obj.ToString());

			if (obj is Closure)
				return DynValue.NewClosure((Closure)obj);

			if (NumericConversions.NumericTypes.Contains(t))
				return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

			if (obj is Table)
				return DynValue.NewTable((Table)obj);

			if (obj is CallbackFunction)
				return DynValue.NewCallback((CallbackFunction)obj);

			if (obj is Delegate)
			{
				Delegate d = (Delegate)obj;


#if NETFX_CORE
				MethodInfo mi = d.GetMethodInfo();
#else
				MethodInfo mi = d.Method;
#endif

				if (CallbackFunction.CheckCallbackSignature(mi, false))
					return DynValue.NewCallback((Func<ScriptExecutionContext, CallbackArguments, DynValue>)d);
			}

			return null;
		}


		/// <summary>
		/// Tries to convert a CLR object to a MoonSharp value, using more in-depth analysis
		/// </summary>
		internal static DynValue ObjectToDynValue<T>(Script script, T obj)
		{
			DynValue v = TryObjectToSimpleDynValue(script, obj);

			if (v != null) return v;

			v = UserData.Create(obj);
			if (v != null) return v;

			if (obj is Type)
				v = UserData.CreateStatic(obj as Type);

			// unregistered enums go as integers
			if (obj is Enum)
				return DynValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(obj.GetType()), obj));

			if (v != null) return v;

			if (obj is Delegate)
				return DynValue.NewCallback(CallbackFunction.FromDelegate(script, (Delegate)(object)obj));

			if (obj is MethodInfo)
			{
				MethodInfo mi = (MethodInfo)(object)obj;

				if (mi.IsStatic)
				{
					return DynValue.NewCallback(CallbackFunction.FromMethodInfo(script, mi));
				}
			}

			if (obj is System.Collections.IList)
			{
				Table t = TableConversions.ConvertIListToTable(script, (System.Collections.IList)obj);
				return DynValue.NewTable(t);
			}

			if (obj is System.Collections.IDictionary)
			{
				Table t = TableConversions.ConvertIDictionaryToTable(script, (System.Collections.IDictionary)obj);
				return DynValue.NewTable(t);
			}

			var enumerator = EnumerationToDynValue(script, obj);
			if (enumerator != null) return enumerator;


			throw ScriptRuntimeException.ConvertObjectFailed(obj);
		}


        /// <summary>
        /// Tries to convert a CLR object to a MoonSharp value, using more in-depth analysis
        /// </summary>
        internal static DynValue GenericToDynValue<T>(Script script, T value)
        {
            if (typeof (T).IsValueType)
            {
                return ValueTypeToDynValue(script, value);
            }
            return ObjectToDynValue(script, value);
        }

        /// <summary>
        /// Tries to convert a CLR object to a value type, using "trivial" logic.
        /// Skips on custom conversions, etc.
        /// Does NOT throw on failure.
        /// </summary>
        internal static DynValue ValueTypeToDynValue<T>(Script script, T value)
        {

            Type t = value.GetType();

            if (value is bool)
                return DynValue.NewBoolean(ValueConverter<T,bool>.Instance.Convert(value));

            if (NumericConversions.NumericTypes.Contains(t) || value is Enum)
                return DynValue.NewNumber(ValueConverter<T, double>.Instance.Convert(value));


            return null;
        }


        /// <summary>
        /// Converts an IEnumerable or IEnumerator to a DynValue
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public static DynValue EnumerationToDynValue(Script script, object obj)
		{
			if (obj is System.Collections.IEnumerable)
			{
				var enumer = (System.Collections.IEnumerable)obj;
				return EnumerableWrapper.ConvertIterator(script, enumer.GetEnumerator());
			}

			if (obj is System.Collections.IEnumerator)
			{
				var enumer = (System.Collections.IEnumerator)obj;
				return EnumerableWrapper.ConvertIterator(script, enumer);
			}

			return null;
		}


        /// <summary>
        /// Tries to convert a CLR object to a MoonSharp value, using more in-depth analysis
        /// </summary>
        internal static DynValue StructToDynValue<T>(Script script, T obj) where T : struct
        {
            DynValue v = TryObjectToSimpleDynValue(script, obj);

            throw ScriptRuntimeException.ConvertObjectFailed(obj);
        }

    }
    public sealed class ValueConverter<TIn, TOut>
    {
        public static readonly ValueConverter<TIn, TOut> Instance = new ValueConverter<TIn, TOut>();

        public Func<TIn, TOut> Convert { get; }

        private ValueConverter()
        {
            var t = typeof(TIn);
            var paramExpr = Expression.Parameter(typeof(TIn), "ValueToBeConverted");

            Convert = (TIn TIn) => default(TOut);

            if (typeof(TIn) == typeof(TOut))
            {
                Convert = Expression.Lambda<Func<TIn, TOut>>(paramExpr,paramExpr).Compile();
            }
            else if (typeof (TIn).IsValueType)
            {
                if (IsPrimitiveConversion(typeof(TIn), typeof(TOut)) ||
                     IsReferenceConversion(typeof(TIn), typeof(TOut)) ||
                     GetUserDefinedCoercion(typeof(TIn), typeof(TOut)))
                {
                    var p = Expression.Parameter(typeof(TIn), "in");
                    var c = Expression.ConvertChecked(p, typeof(TOut));
                    Convert = Expression.Lambda<Func<TIn, TOut>>(c, p).Compile();
                }
            }
            else
            {
                if (IsPrimitiveConversion(typeof (TIn), typeof (TOut)) ||
                    IsReferenceConversion(typeof (TIn), typeof (TOut)) ||
                    GetUserDefinedCoercion(typeof (TIn), typeof (TOut)))
                {
                    var p = Expression.Parameter(typeof (TIn), "in");
                    var c = Expression.Convert(p, typeof (TOut));
                    Convert = Expression.Lambda<Func<TIn, TOut>>(c, p).Compile();
                }
                else if(!typeof(TOut).IsValueType)
                {
                    var p = Expression.Parameter(typeof(TIn), "in");
                    var c = Expression.TypeAs(p, typeof(TOut));
                    Convert = Expression.Lambda<Func<TIn, TOut>>(c, p).Compile();
                }
            }
        }

        internal static bool IsPrimitiveConversion(Type type, Type target)
        {
            if (IsNullableType(type) && target == GetNonNullableType(type))
                return true;

            if (IsNullableType(target) && type == GetNonNullableType(target))
                return true;

            if (IsConvertiblePrimitive(type) && IsConvertiblePrimitive(target))
                return true;

            return false;
        }

        static bool IsConvertiblePrimitive(Type type)
        {
            var t = GetNonNullableType(type);

            if (t == typeof(bool))
                return false;

            if (t.IsEnum)
                return true;

            return t.IsPrimitive;
        }

        private static bool IsNullableType(Type type)
        {
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition() == typeof(Nullable<>);
            return false;
        }

        internal static Type GetNonNullableType(Type type)
        {
            if (IsNullableType(type))
                type = type.GetGenericArguments()[0];
            return type;
        }

        internal static bool IsReferenceConversion(Type type, Type target)
        {
            if (type == typeof(object) || target == typeof(object))
                return true;

            //if (type.IsInterface || target.IsInterface)
                //return true;

            if (type.IsValueType || target.IsValueType)
                return false;

            if (target.IsAssignableFrom(type) || type.IsAssignableFrom(target))
                return true;

            return false;
        }

        private static bool GetUserDefinedCoercion(Type sourceType, Type convertToType)
        {
            Type nonNullableType1 = GetNonNullableType(sourceType);
            Type nonNullableType2 = GetNonNullableType(convertToType);
            MethodInfo[] methods1 = nonNullableType1.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo conversionOperator1 = FindConversionOperator(methods1, sourceType, convertToType);
            if (conversionOperator1 != null)
                return true;

            MethodInfo[] methods2 = nonNullableType2.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo conversionOperator2 = FindConversionOperator(methods2, typeof(TIn), convertToType);
            if (conversionOperator2 != null)
                return true;
            if (nonNullableType1 != typeof(TIn) || nonNullableType2 != convertToType)
            {
                MethodInfo method = FindConversionOperator(methods1, nonNullableType1, nonNullableType2) ?? FindConversionOperator(methods2, nonNullableType1, nonNullableType2);
                if (method != null)
                    return true;
            }
            return false;
        }


        private static MethodInfo FindConversionOperator(MethodInfo[] methods, Type typeFrom, Type typeTo)
        {
            foreach (MethodInfo methodInfo in methods)
            {
                if ((!(methodInfo.Name != "op_Implicit") || !(methodInfo.Name != "op_Explicit")) && (methodInfo.ReturnType == typeTo && methodInfo.GetParameters()[0].ParameterType == typeFrom))
                    return methodInfo;
            }
            return (MethodInfo)null;
        }
    }
}
