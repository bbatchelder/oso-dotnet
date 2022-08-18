
using Microsoft.Extensions.Options;
using Oso.DataFiltering;

namespace Oso
{
    public partial class OsoBuilder
    {
        private readonly Oso _oso;
        public OsoBuilder(Oso oso)
        {
            _oso = oso;
        }

        public void RegisterClass<T>() {
            _oso.RegisterClass(typeof(T));
        }
        public void LoadFiles(params string[] filename) {
            _oso.LoadFiles(filename);
        }

        public void SetDataFilterAdapter(IDataFilterAdapter adapter)
        {
            _oso.Host.Adapter = adapter;
        }
    }
}