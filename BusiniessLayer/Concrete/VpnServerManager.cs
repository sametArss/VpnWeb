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

        public List<VpnServer> GetActiveServers()
        {
            return _vpnRepo
                .GetAllFilterAsync(x => x.IsActive)
                .Result;
        }
    }
}
