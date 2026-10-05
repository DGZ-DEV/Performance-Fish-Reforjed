using System;
using System.Reflection;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Lightweight wrapper struct for type handles implementing IEquatable so they can be used as keys in RefCache.
    /// </summary>
    public readonly struct TypeHandleKey : IEquatable<TypeHandleKey>
    {
        public readonly RuntimeTypeHandle Handle;

        public TypeHandleKey(RuntimeTypeHandle handle)
        {
            Handle = handle;
        }

        public bool Equals(TypeHandleKey other) => Handle.Equals(other.Handle);
        public override bool Equals(object obj) => obj is TypeHandleKey other && Equals(other);
        public override int GetHashCode() => Handle.GetHashCode();
    }

    /// <summary>
    /// Struct wrapper holding a reference value (like string, FieldInfo, MethodInfo, PropertyInfo) 
    /// so it satisfies the 'struct' constraint required by RefCache.
    /// </summary>
    public struct RefBox<T> where T : class
    {
        public T Value;
        public RefBox(T value) => Value = value;
    }
}
