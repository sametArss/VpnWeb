using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BusiniessLayer.Concrete
{
    public class VpnServerManager : IVpnServerService
    {
        private readonly IVpnServerDal _vpnRepo;
        private readonly ILogger<VpnServerManager> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VpnServerManager(IVpnServerDal vpnRepo, ILogger<VpnServerManager> logger, IHttpContextAccessor httpContextAccessor)
        {
            _vpnRepo = vpnRepo;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetIp() => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        private string? GetEmail() => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

        public async Task AddVpnServerAsync(VpnServer vpnServer)
        {
            var ip = GetIp();
            _logger.LogInformation("[SERVER] Add server | Email={Email} Name={Name} ServerIp={ServerIp} IP={Ip}", GetEmail(), vpnServer.Name, vpnServer.IpAddress, ip);
            await _vpnRepo.InsertAsync(vpnServer);
        }

        public async Task DeleteVpnServerAsync(int id)
        {
            var ip = GetIp();
            var vpnServer = await _vpnRepo.GetByIdAsync(id);
            if (vpnServer != null)
            {
                _logger.LogInformation("[SERVER] Delete server | Email={Email} Name={Name} ServerId={Id} IP={Ip}", GetEmail(), vpnServer.Name, id, ip);
                await _vpnRepo.DeleteAsync(vpnServer);
            }
            else
            {
                _logger.LogWarning("[SERVER] Delete failed - not found | Email={Email} ServerId={Id} IP={Ip}", GetEmail(), id, ip);
            }
        }

        public async Task<List<VpnServer>> GetActiveServersAsync()
        {
            return await _vpnRepo.GetAllFilterAsync(x => x.IsActive);
        }

        public async Task<VpnServer> GetByIdAsync(int id)
        {
            return await _vpnRepo.GetByIdAsync(id);
        }

        public async Task UpdateVpnServerAsync(VpnServer vpnServer)
        {
            var ip = GetIp();
            var existing = await _vpnRepo.GetByIdAsync(vpnServer.Id);
            if (existing == null)
            {
                _logger.LogWarning("[SERVER] Update failed - not found | Email={Email} ServerId={Id} IP={Ip}", GetEmail(), vpnServer.Id, ip);
                return;
            }

            _logger.LogInformation("[SERVER] Update server | Email={Email} Name={Name} ServerId={Id} IP={Ip}", GetEmail(), vpnServer.Name, vpnServer.Id, ip);

            existing.Name = vpnServer.Name;
            existing.Country = vpnServer.Country;
            existing.IpAddress = vpnServer.IpAddress;
            existing.SshPort = vpnServer.SshPort;
            existing.SshUser = vpnServer.SshUser;
            existing.IsActive = vpnServer.IsActive;
            existing.CreatedAt = vpnServer.CreatedAt;
            existing.PrivateKeyPath = vpnServer.PrivateKeyPath;
            existing.LatencyMs = vpnServer.LatencyMs;
            existing.LoadPercent = vpnServer.LoadPercent;

            await _vpnRepo.UpdateAsync(existing);
        }
    }
}
