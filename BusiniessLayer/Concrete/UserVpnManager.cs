using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Renci.SshNet;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security.Claims;

namespace BusiniessLayer.Concrete
{
    public class UserVpnManager : IUserVpnService
    {
        private readonly IUserVpnDal _userVpnRepo;
        private readonly IVpnServerDal _vpnServerRepo;
        private readonly ILogger<UserVpnManager> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserVpnManager(
            IUserVpnDal userVpnRepo,
            IVpnServerDal vpnServerRepo,
            ILogger<UserVpnManager> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userVpnRepo = userVpnRepo;
            _vpnServerRepo = vpnServerRepo;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetIp() => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        private string? GetEmail() => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

        private string GenerateClientName(Guid userId)
        {
            var name = $"user_{userId.ToString("N").Substring(0, 8)}";

            if (!Regex.IsMatch(name, @"^user_[a-z0-9]{8}$"))
                throw new Exception("Geçersiz client name formatı.");

            return name;
        }

        public async Task<bool> HasActiveVpnAsync(Guid userId)
        {
            var result = await _userVpnRepo.GetAllFilterAsync(x => x.UserId == userId && x.IsActive);
            return result.Any();
        }

        public async Task<UserVpn> GetActiveVpnAsync(Guid userId)
        {
            return await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive,
                x => x.VpnServer
            );
        }

        private async Task<string> GetNextFreeIpAsync(int vpnServerId)
        {
            var usedIps = await _userVpnRepo.GetAllFilterAsync(
                x => x.VpnServerId == vpnServerId && x.IsActive
            );

            var usedOctets = usedIps
                .Where(x => !string.IsNullOrEmpty(x.ClientIp) && x.ClientIp != "Dynamic (OpenVPN)")
                .Select(x =>
                {
                    var parts = x.ClientIp.Split('.');
                    return parts.Length == 4 && int.TryParse(parts[3], out var o) ? o : 0;
                })
                .ToHashSet();

            for (int octet = 2; octet <= 253; octet++)
            {
                if (!usedOctets.Contains(octet))
                {
                    var ip = $"10.66.66.{octet}";

                    if (!IPAddress.TryParse(ip, out _))
                        throw new Exception("IP oluşturulamadı.");

                    return ip;
                }
            }

            throw new Exception("Sunucuda boş IP kalmadı.");
        }

        private Renci.SshNet.ConnectionInfo CreateConnection(VpnServer server)
        {
            if (!File.Exists(server.PrivateKeyPath))
                throw new Exception("SSH key bulunamadı.");

            var keyFile = new PrivateKeyFile(server.PrivateKeyPath);
            var auth = new PrivateKeyAuthenticationMethod(server.SshUser, keyFile);

            return new Renci.SshNet.ConnectionInfo(
                server.IpAddress,
                server.SshPort,
                server.SshUser,
                auth)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        private async Task<string> DownloadConfig(SftpClient sftp, string remotePath)
        {
            if (!sftp.Exists(remotePath))
                throw new Exception("Config dosyası bulunamadı.");

            var attr = sftp.GetAttributes(remotePath);

            if (attr.Size < 200 || attr.Size > 50_000)
                throw new Exception("Config dosyası boyutu geçersiz.");

            using var ms = new MemoryStream();
            sftp.DownloadFile(remotePath, ms);

            var config = Encoding.UTF8.GetString(ms.ToArray());

            if (!config.Contains("[Interface]") && !config.Contains("client"))
                throw new Exception("Config içeriği geçersiz.");

            return config;
        }

        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId, VpnProtocol protocol)
        {
            _logger.LogInformation(
                "[VPN] Connect request | UserId={UserId} Email={Email} ServerId={ServerId} Protocol={Protocol} IP={Ip}",
                userId, GetEmail(), vpnServerId, protocol, GetIp());

            if (await HasActiveVpnAsync(userId))
                throw new Exception("Zaten aktif VPN bağlantınız var.");

            var server = await _vpnServerRepo.GetByIdAsync(vpnServerId);

            if (server == null)
                throw new Exception("VPN sunucusu bulunamadı.");

            var clientName = GenerateClientName(userId);

            string clientIp;
            string commandText;
            string remotePath;

            if (protocol == VpnProtocol.WireGuard)
            {
                clientIp = await GetNextFreeIpAsync(vpnServerId);

                commandText = $"sudo /etc/wireguard/add_peer.sh {clientName} {clientIp}";
                remotePath = server.SshUser == "root"
                    ? $"/root/{clientName}.conf"
                    : $"/home/{server.SshUser}/{clientName}.conf";
            }
            else
            {
                clientIp = "Dynamic (OpenVPN)";

                commandText = $"sudo /root/add_ovpn_peer.sh {clientName}";
                remotePath = server.SshUser == "root"
                    ? $"/root/{clientName}.ovpn"
                    : $"/home/{server.SshUser}/{clientName}.ovpn";
            }

            string configText;

            try
            {
                var connection = CreateConnection(server);

                using var ssh = new SshClient(connection);
                using var sftp = new SftpClient(connection);

                int retry = 0;

                while (retry < 3)
                {
                    try
                    {
                        ssh.Connect();
                        break;
                    }
                    catch
                    {
                        retry++;

                        if (retry >= 3)
                            throw new Exception("VPN sunucusuna erişilemiyor.");

                        await Task.Delay(1000);
                    }
                }

                var cmd = ssh.CreateCommand(commandText);
                cmd.CommandTimeout = TimeSpan.FromSeconds(20);

                cmd.Execute();

                if (cmd.ExitStatus != 0)
                {
                    _logger.LogError("[VPN] SSH script error | UserId={UserId} Email={Email} Error={Error}", userId, GetEmail(), cmd.Error);
                    throw new Exception("VPN oluşturulamadı.");
                }

                ssh.Disconnect();

                sftp.Connect();

                configText = await DownloadConfig(sftp, remotePath);

                sftp.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VPN] Connect error - SSH failed | UserId={UserId} Email={Email} ServerId={ServerId} IP={Ip}", userId, GetEmail(), vpnServerId, GetIp());
                throw new Exception("VPN bağlantısı kurulamadı.");
            }

            try
            {
                await _userVpnRepo.InsertAsync(new UserVpn
                {
                    UserId = userId,
                    VpnServerId = vpnServerId,
                    ClientIp = clientIp,
                    Protocol = protocol,
                    ConnectedAt = DateTime.UtcNow,
                    IsActive = true,
                    ClientConfig = configText
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VPN] Connect error - DB insert failed | UserId={UserId} Email={Email} IP={Ip}", userId, GetEmail(), GetIp());
                throw new Exception("VPN kaydı oluşturulamadı.");
            }

            _logger.LogInformation("[VPN] Connected | UserId={UserId} Email={Email} ServerId={ServerId} Protocol={Protocol} IP={Ip}", userId, GetEmail(), vpnServerId, protocol, GetIp());
        }

        public async Task DisconnectAsync(Guid userId)
        {
            var activeVpn = await GetActiveVpnAsync(userId);

            if (activeVpn == null)
                return;

            var server = activeVpn.VpnServer;

            try
            {
                var connection = CreateConnection(server);

                using var ssh = new SshClient(connection);

                ssh.Connect();

                var clientName = GenerateClientName(userId);

                var command = activeVpn.Protocol == VpnProtocol.WireGuard
                    ? $"sudo /etc/wireguard/remove_peer.sh {clientName}"
                    : $"sudo /root/remove_ovpn_peer.sh {clientName}";

                var cmd = ssh.RunCommand(command);

                if (cmd.ExitStatus != 0)
                    _logger.LogWarning("[VPN] Disconnect warning - peer remove script error | UserId={UserId} Email={Email}", userId, GetEmail());

                ssh.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VPN] Disconnect error - peer remove failed | UserId={UserId} Email={Email} IP={Ip}", userId, GetEmail(), GetIp());
            }

            activeVpn.IsActive = false;
            await _userVpnRepo.UpdateAsync(activeVpn);

            _logger.LogInformation("[VPN] Disconnected | UserId={UserId} Email={Email} IP={Ip}", userId, GetEmail(), GetIp());
        }
    }
}