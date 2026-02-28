using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusiniessLayer.Concrete
{
    public class UserVpnManager : IUserVpnService
    {
        private readonly IUserVpnDal _userVpnRepo;
        private readonly IVpnServerDal _vpnServerRepo;
        private readonly ILogger<UserVpnManager> _logger;

        public UserVpnManager(IUserVpnDal userVpnRepo, IVpnServerDal vpnServerRepo, ILogger<UserVpnManager> logger)
        {
            _userVpnRepo = userVpnRepo;
            _vpnServerRepo = vpnServerRepo;
            _logger = logger;
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
                .Select(x => {
                    var parts = x.ClientIp.Split('.');
                    return parts.Length == 4 && int.TryParse(parts[3], out var o) ? o : 0;
                })
                .ToHashSet();

            for (int octet = 2; octet <= 253; octet++)
            {
                if (!usedOctets.Contains(octet))
                    return $"10.66.66.{octet}";
            }

            throw new Exception("Bu sunucuda boş IP kalmadı.");
        }

        // DB insert fail olursa sunucudaki peer'ı sil
        private async Task RollbackPeerAsync(VpnServer vpnServer, Guid userId, VpnProtocol protocol)
        {
            try
            {
                using var keyFile = new PrivateKeyFile(vpnServer.PrivateKeyPath);
                var auth = new PrivateKeyAuthenticationMethod(vpnServer.SshUser, keyFile);
                var connection = new ConnectionInfo(
                    vpnServer.IpAddress,
                    vpnServer.SshPort,
                    vpnServer.SshUser,
                    auth)
                { Timeout = TimeSpan.FromSeconds(5) };

                using var ssh = new SshClient(connection);
                ssh.Connect();

                var clientName = $"user_{userId.ToString("N").Substring(0, 8)}";
                var command = protocol == VpnProtocol.WireGuard
                    ? $"sudo /etc/wireguard/remove_peer.sh {clientName}"
                    : $"sudo /root/remove_ovpn_peer.sh {clientName}";

                ssh.RunCommand(command);
                ssh.Disconnect();

                _logger.LogWarning("Peer rollback başarılı. User: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Peer rollback BAŞARISIZ! Manuel müdahale gerekiyor. User: {UserId}", userId);
            }
        }

        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId, VpnProtocol protocol)
        {
            _logger.LogInformation("VPN Connect isteği. User: {UserId}, Server: {ServerId}, Protocol: {Protocol}",
                userId, vpnServerId, protocol);

            var existing = await _userVpnRepo.GetAllFilterAsync(x => x.UserId == userId && x.IsActive);
            if (existing.Any())
                throw new Exception("Zaten aktif bir VPN bağlantınız var.");

            var targetServer = await _vpnServerRepo.GetByIdAsync(vpnServerId);
            if (targetServer == null)
                throw new Exception("Sunucu bulunamadı.");

            var clientName = $"user_{userId.ToString("N").Substring(0, 8)}";

            if (!System.Text.RegularExpressions.Regex.IsMatch(clientName, @"^user_[a-z0-9]{8}$"))
                throw new Exception("Geçersiz kullanıcı adı formatı.");

            string clientIp, commandText, remotePath;

            if (protocol == VpnProtocol.WireGuard)
            {
                clientIp = await GetNextFreeIpAsync(vpnServerId);
                commandText = $"sudo /etc/wireguard/add_peer.sh {clientName} {clientIp}";
                remotePath = targetServer.SshUser == "root"
                    ? $"/root/{clientName}.conf"
                    : $"/home/{targetServer.SshUser}/{clientName}.conf";
            }
            else if (protocol == VpnProtocol.OpenVPN)
            {
                clientIp = "Dynamic (OpenVPN)";
                commandText = $"sudo /root/add_ovpn_peer.sh {clientName}";
                remotePath = targetServer.SshUser == "root"
                    ? $"/root/{clientName}.ovpn"
                    : $"/home/{targetServer.SshUser}/{clientName}.ovpn";
            }
            else
            {
                throw new Exception("Desteklenmeyen protokol.");
            }

            if (!File.Exists(targetServer.PrivateKeyPath))
            {
                _logger.LogError("SSH Key dosyası bulunamadı. ServerId: {ServerId}", vpnServerId);
                throw new Exception("Sunucu yapılandırma hatası.");
            }

            string configText;

            try
            {
                using var keyFile = new PrivateKeyFile(targetServer.PrivateKeyPath);
                var auth = new PrivateKeyAuthenticationMethod(targetServer.SshUser, keyFile);
                var connection = new ConnectionInfo(
                    targetServer.IpAddress,
                    targetServer.SshPort,
                    targetServer.SshUser,
                    auth)
                { Timeout = TimeSpan.FromSeconds(5) };

                using var ssh = new SshClient(connection);
                using var sftp = new SftpClient(connection);

                int retry = 0;
                while (retry < 3)
                {
                    try { ssh.Connect(); break; }
                    catch
                    {
                        retry++;
                        _logger.LogWarning("SSH bağlantı denemesi {Retry} başarısız.", retry);
                        if (retry >= 3) throw new Exception("VPN sunucusuna erişilemiyor.");
                        await Task.Delay(1000);
                    }
                }

                var cmd = ssh.CreateCommand(commandText);
                cmd.CommandTimeout = TimeSpan.FromSeconds(20);
                cmd.Execute();

                if (cmd.ExitStatus != 0)
                {
                    _logger.LogError("Script hatası. ServerId: {ServerId}, Error: {Error}", vpnServerId, cmd.Error);
                    throw new Exception("VPN oluşturulamadı.");
                }

                ssh.Disconnect();

                sftp.Connect();

                if (!sftp.Exists(remotePath))
                    throw new Exception("VPN yapılandırması oluşturulamadı.");

                var fileAttr = sftp.GetAttributes(remotePath);
                if (fileAttr.Size < 200 || fileAttr.Size > 50_000)
                    throw new Exception("Config dosyası geçersiz.");

                using var ms = new MemoryStream();
                sftp.DownloadFile(remotePath, ms);
                configText = Encoding.UTF8.GetString(ms.ToArray());
                sftp.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VPN oluşturma sürecinde hata. User: {UserId}", userId);
                throw new Exception("VPN bağlantısı kurulamadı. Lütfen tekrar deneyin.");
            }

            // DB kaydı
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
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "DB insert başarısız. Peer rollback başlatılıyor. User: {UserId}", userId);
                await RollbackPeerAsync(targetServer, userId, protocol); // ✅ Direkt sunucuya gidiyor
                throw new Exception("VPN kurulamadı. Lütfen tekrar deneyin.");
            }

            _logger.LogInformation("User {UserId} başarıyla {Protocol} üzerinden bağlandı.", userId, protocol);
        }

        public async Task DisconnectAsync(Guid userId)
        {
            _logger.LogInformation("Disconnect isteği. User: {UserId}", userId);

            var activeVpn = await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive,
                x => x.VpnServer
            );

            if (activeVpn == null)
            {
                _logger.LogWarning("User {UserId} için aktif VPN bulunamadı.", userId);
                return;
            }

            var vpnServer = activeVpn.VpnServer;

            if (vpnServer != null)
            {
                try
                {
                    using var keyFile = new PrivateKeyFile(vpnServer.PrivateKeyPath);
                    var auth = new PrivateKeyAuthenticationMethod(vpnServer.SshUser, keyFile);
                    var connection = new ConnectionInfo(
                        vpnServer.IpAddress,
                        vpnServer.SshPort,
                        vpnServer.SshUser,
                        auth)
                    { Timeout = TimeSpan.FromSeconds(5) };

                    using var ssh = new SshClient(connection);
                    ssh.Connect();

                    var clientName = $"user_{userId.ToString("N").Substring(0, 8)}";
                    var command = activeVpn.Protocol == VpnProtocol.WireGuard
                        ? $"sudo /etc/wireguard/remove_peer.sh {clientName}"
                        : $"sudo /root/remove_ovpn_peer.sh {clientName}";

                    var cmd = ssh.RunCommand(command);
                    if (cmd.ExitStatus != 0)
                        _logger.LogWarning("Peer silinirken uyarı. ServerId: {ServerId}", vpnServer.Id);

                    ssh.Disconnect();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SSH ile silme yapılamadı. User: {UserId}", userId);
                }
            }

            // SSH fail olsa bile DB güncellenir
            activeVpn.IsActive = false;
            await _userVpnRepo.UpdateAsync(activeVpn);
            _logger.LogInformation("User {UserId} bağlantısı sonlandırıldı.", userId);
        }
    }
}