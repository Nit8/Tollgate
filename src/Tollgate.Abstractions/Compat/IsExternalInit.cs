// netstandard2.0 does not ship the IsExternalInit type that the C# compiler
// emits for `init` accessors and `record` types. This shim provides it so
// Tollgate.Abstractions compiles unchanged for every .NET runtime of the
// last decade. NET6_0+ targets ignore this file entirely.
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
