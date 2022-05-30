using System.Runtime.InteropServices;

namespace Oso.Ffi;

[StructLayout(LayoutKind.Sequential)]
internal readonly ref struct StringResult
{
    public readonly IntPtr result;
    public readonly IntPtr error;
}