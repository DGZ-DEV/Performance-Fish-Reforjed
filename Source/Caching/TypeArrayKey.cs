using System;
using System.Reflection;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Helper record struct wrapper for array of types used in composite keys for method/constructor lookups.
    /// </summary>
    public readonly struct TypeArrayKey : IEquatable<TypeArrayKey>
    {
        public readonly Type[] Types;
        private readonly int _hashCode;

        public TypeArrayKey(Type[] types)
        {
            Types = types ?? Array.Empty<Type>();
            int hash = 17;
            for (int i = 0; i < Types.Length; i++)
            {
                hash = hash * 31 + (Types[i]?.GetHashCode() ?? 0);
            }
            _hashCode = hash;
        }

        public bool Equals(TypeArrayKey other)
        {
            if (ReferenceEquals(Types, other.Types))
                return true;
            if (Types.Length != other.Types.Length)
                return false;
            for (int i = 0; i < Types.Length; i++)
            {
                if (Types[i] != other.Types[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is TypeArrayKey other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }
}
