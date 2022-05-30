using System.Runtime.InteropServices;

namespace Oso.Ffi;
internal class QueryHandle : SafeHandle
{
    public QueryHandle() : base(IntPtr.Zero, true) { }
    public QueryHandle(IntPtr ptr) : base(IntPtr.Zero, true)
    {
        handle = ptr;
    }

    public override bool IsInvalid
    {
        get { return this.handle == IntPtr.Zero; }
    }

    protected override bool ReleaseHandle()
    {
        if (!this.IsInvalid)
        {
            _ = Native.query_free(handle);
        }

        return true;
    }
}