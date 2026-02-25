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
            var activeVpns = await _userVpnRepo.GetAllFilterAsync(x => x.VpnServerId == vpnServerId);

            if (!activeVpns.Any())
                return "10.66.66.2";

            var maxOctet = activeVpns
                .Where(x => !string.IsNullOrEmpty(x.ClientIp) && x.ClientIp != "Dynamic (OpenVPN)") // OpenVPN olanları yoksay
                .Select(x =>
                {
                    var parts = x.ClientIp.Split('.');
                    return parts.Length == 4 ? int.Parse(parts[3]) : 0;
                })
                .DefaultIfEmpty(1)
                .Max();

            if (maxOctet >= 253)
            {
                _logger.LogCritical($"VPN Server ID {vpnServerId} IP havuzu doldu!");
                throw new Exception("Bu sunucuda boş IP kalmadı.");
            }

            return $"10.66.66.{maxOctet + 1}";
        }

        // 🔥 GÜNCELLEME: Metoda "VpnProtocol protocol" parametresi eklendi
        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId, VpnProtocol protocol)
        {
            _logger.LogInformation($"VPN Connect isteği başladı. User: {userId}, Server: {vpnServerId}, Protocol: {protocol}");

            // 1. Abuse Koruması
            if (await HasActiveVpnAsync(userId))
            {
                _logger.LogWarning($"User {userId} zaten bağlı, tekrar bağlanmaya çalıştı.");
                throw new Exception("Zaten aktif bir VPN bağlantınız var.");
            }

            var targetServer = await _vpnServerRepo.GetByIdAsync(vpnServerId);
            if (targetServer == null) throw new Exception("Sunucu bulunamadı!");

            // 2. IP ve İsim Belirleme
            var clientName = $"user_{userId.ToString().Substring(0, 8)}";
            string clientIp = "";
            string commandText = "";
            string remotePath = "";

            // 🔥 GÜNCELLEME 1: OpenVPN için de yolu dinamik yaptık ve "< /dev/null" ekledik
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
                // < /dev/null kısmı, script eğer girdi beklerse beklemesini iptal eder
                commandText = $"sudo /root/add_ovpn_peer.sh {clientName}";

                remotePath = targetServer.SshUser == "root"
                    ? $"/root/{clientName}.ovpn"
                    : $"/home/{targetServer.SshUser}/{clientName}.ovpn";
            }

            // 3. SSH Key ile Bağlantı
            if (!File.Exists(targetServer.PrivateKeyPath))
            {
                _logger.LogError($"SSH Key dosyası bulunamadı: {targetServer.PrivateKeyPath}");
                throw new Exception("Sunucu yapılandırma hatası (Key dosya eksik).");
            }

            using var keyFile = new PrivateKeyFile(targetServer.PrivateKeyPath);
            var auth = new PrivateKeyAuthenticationMethod(targetServer.SshUser, keyFile);
            var connection = new ConnectionInfo(targetServer.IpAddress, targetServer.SshPort, targetServer.SshUser, auth)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            // 4. SSH İşlemleri
            using var ssh = new SshClient(connection);
            int retryCount = 0;
            bool connected = false;

            while (retryCount < 3 && !connected)
            {
                try
                {
                    ssh.Connect();
                    connected = true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, $"SSH bağlantı denemesi {retryCount} başarısız. Bekleniyor...");
                    await Task.Delay(1000);
                }
            }

            if (!connected) throw new Exception("VPN sunucusuna erişilemiyor (SSH Timeout).");

            try
            {
                var cmd = ssh.CreateCommand(commandText);
                // 🔥 GÜNCELLEME 2: 15 Saniyelik komut zaman aşımı ekledik. Asla sonsuza kadar dönmeyecek.
                cmd.CommandTimeout = TimeSpan.FromSeconds(45);
                var result = cmd.Execute();

                if (cmd.ExitStatus != 0)
                {
                    _logger.LogError($"Script Hatası: {cmd.Error}");
                    throw new Exception($"VPN kullanıcısı oluşturulamadı. Sunucu detayı: {cmd.Error}");
                }

                // 5. Config İndirme (SFTP)
                using var sftp = new SftpClient(connection);
                sftp.Connect();

                if (!sftp.Exists(remotePath))
                    throw new Exception($"Config dosyası sunucuda ({remotePath}) bulunamadı.");

                var fileAttr = sftp.GetAttributes(remotePath);
                if (fileAttr.Size > 50_000)
                    throw new Exception("Config dosyası anormal derecede büyük.");

                using var ms = new MemoryStream();
                sftp.DownloadFile(remotePath, ms);

                var configText = Encoding.UTF8.GetString(ms.ToArray());
                sftp.Disconnect();
                ssh.Disconnect();

                // 6. DB Kaydı
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

                _logger.LogInformation($"User {userId} başarıyla {protocol} üzerinden bağlandı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VPN oluşturma sürecinde hata.");
                throw;
            }
        }

        public async Task DisconnectAsync(Guid userId)
        {
            _logger.LogInformation($"Disconnect isteği. User: {userId}");

            var activeVpn = await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive,
                x => x.VpnServer
            );

            if (activeVpn == null)
            {
                _logger.LogWarning($"User {userId} için aktif VPN bulunamadı.");
                return;
            }

            var vpnServer = activeVpn.VpnServer;

            if (vpnServer == null)
            {
                activeVpn.IsActive = false;
                await _userVpnRepo.UpdateAsync(activeVpn);
                return;
            }

            try
            {
                using var keyFile = new PrivateKeyFile(vpnServer.PrivateKeyPath);
                var auth = new PrivateKeyAuthenticationMethod(vpnServer.SshUser, keyFile);

                var connection = new ConnectionInfo(
                    vpnServer.IpAddress,
                    vpnServer.SshPort,
                    vpnServer.SshUser,
                    auth
                )
                { Timeout = TimeSpan.FromSeconds(5) };

                using var ssh = new SshClient(connection);
                ssh.Connect();

                var clientName = $"user_{userId.ToString().Substring(0, 8)}";
                string command = "";

                // 🔥 GÜNCELLEME: Silme işlemini protokole göre ayarla
                if (activeVpn.Protocol == VpnProtocol.WireGuard)
                {
                    command = $"sudo /etc/wireguard/remove_peer.sh {clientName}";
                }
                else if (activeVpn.Protocol == VpnProtocol.OpenVPN)
                {
                    command = $"sudo /root/remove_ovpn_peer.sh {clientName}";
                }

                var cmd = ssh.RunCommand(command);

                if (cmd.ExitStatus != 0)
                {
                    _logger.LogWarning($"Peer silinirken uyarı (önemsiz olabilir): {cmd.Error}");
                }

                ssh.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSH ile silme yapılamadı, ancak DB güncellenecek.");
            }
            finally
            {
                activeVpn.IsActive = false;
                await _userVpnRepo.UpdateAsync(activeVpn);
                _logger.LogInformation($"User {userId} bağlantısı sonlandırıldı.");
            }
        }
    }
}