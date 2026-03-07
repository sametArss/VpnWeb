using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusiniessLayer.Abstract
{
    public interface IVpnServerService
    {
        Task<List<VpnServer>> GetActiveServersAsync();
        Task AddVpnServerAsync(VpnServer vpnServer);
        Task UpdateVpnServerAsync(VpnServer vpnServer);
        Task DeleteVpnServerAsync(int id);
        Task<VpnServer> GetByIdAsync(int id);
    }
}
