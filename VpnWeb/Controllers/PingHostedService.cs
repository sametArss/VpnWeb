using DataAcsessLayer.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VpnWeb.Controllers
{
    public class PingHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public PingHostedService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IVpnServerDal>();
                var userVpnRepo = scope.ServiceProvider.GetRequiredService<IUserVpnDal>();

                var servers = await repo.GetAllAsync();

                foreach (var server in servers)
                {
                    // TCP Ping
                    try
                    {
                        using var tcp = new System.Net.Sockets.TcpClient();
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        await tcp.ConnectAsync(server.IpAddress, server.SshPort);
                        sw.Stop();
                        server.LatencyMs = (int)sw.ElapsedMilliseconds;
                    }
                    catch
                    {
                        server.LatencyMs = null;
                    }

                    // Load Hesapla
                    var activeCount = await userVpnRepo.CountAsync(x => x.VpnServerId == server.Id && x.IsActive);
                    int maxCapacity = 100;
                    server.LoadPercent = (int)((activeCount / (double)maxCapacity) * 100);

                    // Console.WriteLine($"Sunucu: {server.Name} | ms: {server.LatencyMs} | Load: {server.LoadPercent}%");

                    await repo.UpdateAsync(server);
                }

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
    }
}