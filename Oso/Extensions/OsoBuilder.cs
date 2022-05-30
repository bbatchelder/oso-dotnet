
using Microsoft.Extensions.Options;

namespace Oso
{
    public class OsoBuilder
    {
        private readonly Oso _oso;
        public OsoBuilder(Oso oso)
        {
            _oso = oso;
        }

        public void RegisterClass<T>() {
            _oso.RegisterClass(typeof(T));
        }
        public void LoadFiles(string filename) {
            _oso.LoadFiles(filename);
        }
    }
}