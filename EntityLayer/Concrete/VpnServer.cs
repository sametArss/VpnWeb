using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class VpnServer
    {
        public int Id { get; set; }

        public string Name { get; set; }        // Germany-1
        public string Country { get; set; }     // Germany
        public string IpAddress { get; set; }   // 45.xxx.xxx.xxx
        public int SshPort { get; set; }        // 22
        public string SshUser { get; set; }     // vpnadmin

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
