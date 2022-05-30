using System.Runtime.InteropServices;

namespace Oso.Ffi;

[StructLayout(LayoutKind.Sequential)]
internal readonly ref struct QueryResult
{
    public readonly IntPtr result;
    public readonly IntPtr error;
}