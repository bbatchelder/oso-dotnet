using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Oso
{
    public static class OsoServiceCollectionExtensions
    {
        public static IServiceCollection AddOso(this IServiceCollection serviceCollection, Action<OsoBuilder>? builder = null)
        {
            Oso oso = new Oso();
            serviceCollection.AddSingleton<Oso>(oso);
            builder?.Invoke(new OsoBuilder(oso));
            return serviceCollection;
        }

        // public static IServiceCollection AddOsoBuilder(this IServiceCollection serviceCollection, Action<OsoBuilder> builder)
        // {
        //     serviceCollection.AddSingleton<Action<OsoBuilder>>(builder);
        //     return serviceCollection;
        // }
        
    }
}