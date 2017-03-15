﻿using System;
using System.Collections.Generic;

namespace MoonSharp.Interpreter.Interop
{
    internal static class TypedCustomConvertersCollection<T>
    {
        static public Dictionary<Type, Func<DynValue, Type, T>>[] m_Script2Clr = new Dictionary<Type, Func<DynValue, Type, T>>[(int)LuaTypeExtensions.MaxConvertibleTypes + 1];
        static public Dictionary<Type, Func<Script, T, DynValue>> m_Clr2Script = new Dictionary<Type, Func<Script, T, DynValue>>();

        static TypedCustomConvertersCollection()
        {
            for (int i = 0; i < m_Script2Clr.Length; i++)
                m_Script2Clr[i] = new Dictionary<Type, Func<DynValue, Type, T>>();
        }
    }

    /// <summary>
    /// A collection of custom converters between MoonSharp types and CLR types.
    /// If a converter function is not specified or returns null, the standard conversion path applies.
    /// </summary>
    public class CustomConvertersCollection
    {
        private Dictionary<Type, Func<DynValue, Type, object>>[] m_Script2Clr = new Dictionary<Type, Func<DynValue, Type, object>>[(int)LuaTypeExtensions.MaxConvertibleTypes + 1];
        private Dictionary<Type, Func<Script, object, DynValue>> m_Clr2Script = new Dictionary<Type, Func<Script, object, DynValue>>();

        internal CustomConvertersCollection()
        {
            for (int i = 0; i < m_Script2Clr.Length; i++)
                m_Script2Clr[i] = new Dictionary<Type, Func<DynValue, Type, object>>();
        }


        /// <summary>
        /// Sets a custom converter from a script data type to a CLR data type. Set null to remove a previous custom converter.
        /// </summary>
        /// <param name="scriptDataType">The script data type</param>
        /// <param name="clrDataType">The CLR data type.</param>
        /// <param name="converter">The converter, or null.</param>
        public void SetScriptToClrCustomConversion(DataType scriptDataType, Type clrDataType, Func<DynValue, Type, object> converter = null)
        {
            if ((int)scriptDataType > m_Script2Clr.Length)
                throw new ArgumentException("scriptDataType");

            Dictionary<Type, Func<DynValue, Type, object>> map = m_Script2Clr[(int)scriptDataType];

            if (converter == null)
            {
                if (map.ContainsKey(clrDataType))
                    map.Remove(clrDataType);
            }
            else
            {
                map[clrDataType] = converter;
            }
        }


        /// <summary>
        /// Gets a custom converter from a script data type to a CLR data type, or null
        /// </summary>
        /// <param name="scriptDataType">The script data type</param>
        /// <param name="clrDataType">The CLR data type.</param>
        /// <returns>The converter function, or null if not found</returns>
        public Func<DynValue, Type, object> GetScriptToClrCustomConversion(DataType scriptDataType, Type clrDataType)
        {
            if ((int)scriptDataType > m_Script2Clr.Length)
                return null;

            Dictionary<Type, Func<DynValue, Type, object>> map = m_Script2Clr[(int)scriptDataType];
            foreach (var kvp in map)
            {
                Type t = kvp.Key;
                if (t.IsAssignableFrom(clrDataType))
                {
                    return map.GetOrDefault(t);
                }
            }
            return map.GetOrDefault(clrDataType);
        }

        /// <summary>
        /// Gets a custom converter from a script data type to a CLR data type, or null
        /// </summary>
        /// <param name="scriptDataType">The script data type</param>
        /// <param name="clrDataType">The CLR data type.</param>
        /// <returns>The converter function, or null if not found</returns>
        public Func<DynValue, Type, T> GetScriptToClrCustomConversion<T>(DataType scriptDataType)
        {
            if ((int)scriptDataType > m_Script2Clr.Length)
                return null;

            Dictionary<Type, Func<DynValue, Type, T>> map = TypedCustomConvertersCollection<T>.m_Script2Clr[(int)scriptDataType];
            foreach (var kvp in map)
            {
                Type t = kvp.Key;
                if (t.IsAssignableFrom(typeof(T)))
                {
                    return map.GetOrDefault(t);
                }
            }
            return map.GetOrDefault(typeof(T));
        }

        /// <summary>
        /// Sets a custom converter from a CLR data type. Set null to remove a previous custom converter.
        /// </summary>
        /// <param name="clrDataType">The CLR data type.</param>
        /// <param name="converter">The converter, or null.</param>
        public void SetClrToScriptCustomConversion(Type clrDataType, Func<Script, object, DynValue> converter = null)
        {
            if (converter == null)
            {
                if (m_Clr2Script.ContainsKey(clrDataType))
                    m_Clr2Script.Remove(clrDataType);
            }
            else
            {
                m_Clr2Script[clrDataType] = converter;
            }
        }

        /// <summary>
        /// Sets a custom converter from a CLR data type. Set null to remove a previous custom converter.
        /// </summary>
        /// <typeparam name="T">The CLR data type.</typeparam>
        /// <param name="converter">The converter, or null.</param>
        public void SetClrToScriptCustomConversion<T>(Func<Script, T, DynValue> converter = null)
        {
            Type clrDataType = typeof(T);
            Func<Script, object, DynValue> converter1 = (s, o) => converter(s, (T)o);

            if (converter == null)
            {
                if (m_Clr2Script.ContainsKey(clrDataType))
                    m_Clr2Script.Remove(clrDataType);
            }
            else
            {
                m_Clr2Script[clrDataType] = converter1;
            }
        }


        /// <summary>
        /// Gets a custom converter from a CLR data type, or null
        /// </summary>
        /// <param name="clrDataType">Type of the color data.</param>
        /// <returns>The converter function, or null if not found</returns>
        public Func<Script, object, DynValue> GetClrToScriptCustomConversion(Type clrDataType)
        {
            foreach (var kvp in m_Clr2Script)
            {
                Type t = kvp.Key;
                if (t.IsAssignableFrom(clrDataType))
                {
                    return m_Clr2Script.GetOrDefault(t);
                }
            }
            return m_Clr2Script.GetOrDefault(clrDataType);
        }

        /// <summary>
        /// Removes all converters.
        /// </summary>
        public void Clear()
        {
            m_Clr2Script.Clear();

            for (int i = 0; i < m_Script2Clr.Length; i++)
                m_Script2Clr[i].Clear();
        }

    }
}
