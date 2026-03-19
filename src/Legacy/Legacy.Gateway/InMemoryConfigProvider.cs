using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Legacy.Gateway;

/// <summary>
/// Provedor de configuração YARP em memória que suporta atualização em runtime.
/// Permite alternar Blue/Green sem reiniciar o gateway.
/// </summary>
public class InMemoryConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfig _config;

    public InMemoryConfigProvider(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        _config = new InMemoryConfig(routes, clusters);
    }

    public IProxyConfig GetConfig() => _config;

    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var oldConfig = _config;
        _config = new InMemoryConfig(routes, clusters);
        oldConfig.SignalChange();
    }

    private class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        public void SignalChange() => _cts.Cancel();
    }
}
