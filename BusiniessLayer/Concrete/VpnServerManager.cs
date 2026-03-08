using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusiniessLayer.Concrete
{
    public class VpnServerManager : IVpnServerService
    {
        private readonly IVpnServerDal _vpnRepo;

        public VpnServerManager(IVpnServerDal vpnRepo)
        {
            _vpnRepo = vpnRepo;
        }

        public async Task AddVpnServerAsync(VpnServer vpnServer)
        {
            await _vpnRepo.InsertAsync(vpnServer);
        }

        public async Task DeleteVpnServerAsync(int id)
        {
            var vpnServer = await _vpnRepo.GetByIdAsync(id);
            if (vpnServer != null)
                await _vpnRepo.DeleteAsync(vpnServer);
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
            var existing = await _vpnRepo.GetByIdAsync(vpnServer.Id);
            if (existing == null) return;

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
