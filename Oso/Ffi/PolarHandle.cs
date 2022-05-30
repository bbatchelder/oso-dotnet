using System.Runtime.InteropServices;

namespace Oso.Ffi;
internal class PolarHandle : SafeHandle
{
    public PolarHandle() : base(IntPtr.Zero, true) { }

    public override bool IsInvalid
    {
        get { return this.handle == IntPtr.Zero; }
    }

    protected override bool ReleaseHandle()
    {
        if (!this.IsInvalid)
        {
            _ = Native.polar_free(handle);
        }

        return true;
    }
}