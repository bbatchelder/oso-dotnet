using Microsoft.Extensions.DependencyInjection;

namespace Oso
{
    public static class OsoServiceCollectionExtensions
    {
        public static IServiceCollection AddOso(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<Oso>();

            return serviceCollection;
        }
        
    }
}