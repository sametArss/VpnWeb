using EntityLayer.Concrete;
using System;
using System.Threading.Tasks; // Task için bunu eklemeyi unutma

namespace BusiniessLayer.Abstract
{
    public interface IUserVpnService
    {
        // bool -> Task<bool> oldu
        Task<bool> HasActiveVpnAsync(Guid userId); 

        // void -> Task oldu
        Task ConnectUserToVpnAsync(Guid userId, int vpnServerId); 

        // UserVpn -> Task<UserVpn> oldu
        Task<UserVpn> GetActiveVpnAsync(Guid userId); 

        // void -> Task oldu
        Task DisconnectAsync(Guid userId); 
    }
}