using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class UserVpn
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public int VpnServerId { get; set; }
        public VpnServer? VpnServer { get; set; }

        public string? ClientIp { get; set; }
        public DateTime ConnectedAt { get; set; }

        public string ClientConfig { get; set; }

        public bool IsActive { get; set; }
    }
}
