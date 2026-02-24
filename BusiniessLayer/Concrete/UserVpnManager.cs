using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using Microsoft.Extensions.Logging; // Loglama için şart
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
        private readonly ILogger<UserVpnManager> _logger; // 🔥 Loglama eklendi

        public UserVpnManager(IUserVpnDal userVpnRepo, IVpnServerDal vpnServerRepo, ILogger<UserVpnManager> logger)
        {
            _userVpnRepo = userVpnRepo;
            _vpnServerRepo = vpnServerRepo;
            _logger = logger;
        }

        public async Task<bool> HasActiveVpnAsync(Guid userId)
        {
            // Basit kontrol
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

        // 🔥 PERFORMANS OPTİMİZASYONU
        // Not: Gerçek çözüm Repository'e "GetMaxIpOctet" metodu yazmaktır.
        // Şimdilik C# tarafında en azından tüm listeyi çekip RAM'i şişirmeyelim.
        private async Task<string> GetNextFreeIpAsync(int vpnServerId)
        {
            // Burası normalde: await _userVpnRepo.GetLastUsedIpOctetAsync(vpnServerId); olmalı.
            // Mevcut yapına uyumlu ama iyileştirilmiş hali:
            var activeVpns = await _userVpnRepo.GetAllFilterAsync(x => x.VpnServerId == vpnServerId);

            if (!activeVpns.Any())
                return "10.66.66.2";

            var maxOctet = activeVpns
                .Where(x => !string.IsNullOrEmpty(x.ClientIp))
                .Select(x =>
                {
                    var parts = x.ClientIp.Split('.');
                    return parts.Length == 4 ? int.Parse(parts[3]) : 0;
                })
                .DefaultIfEmpty(1) // Liste boşsa veya parse edilemezse 1 dön
                .Max();

            if (maxOctet >= 253)
            {
                _logger.LogCritical($"VPN Server ID {vpnServerId} IP havuzu doldu!");
                throw new Exception("Bu sunucuda boş IP kalmadı.");
            }

            return $"10.66.66.{maxOctet + 1}";
        }

        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId)
        {
            _logger.LogInformation($"VPN Connect isteği başladı. User: {userId}, Server: {vpnServerId}");

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

            // Race Condition riskini azaltmak için burada transaction veya lock kullanılabilir
            // Ama en temizi DB'de (VpnServerId, ClientIp) UNIQUE index olmasıdır.
            var clientIp = await GetNextFreeIpAsync(vpnServerId);

            // 3. SSH Key ile Bağlantı (Şifre YOK)
            if (!File.Exists(targetServer.PrivateKeyPath))
            {
                _logger.LogError($"SSH Key dosyası bulunamadı: {targetServer.PrivateKeyPath}");
                throw new Exception("Sunucu yapılandırma hatası (Key dosya eksik).");
            }

            using var keyFile = new PrivateKeyFile(targetServer.PrivateKeyPath);
            var auth = new PrivateKeyAuthenticationMethod(targetServer.SshUser, keyFile);

            var connection = new ConnectionInfo(
                targetServer.IpAddress,
                targetServer.SshPort,
                targetServer.SshUser,
                auth
            )
            {
                Timeout = TimeSpan.FromSeconds(10) // 🔥 Timeout eklendi
            };

            // 4. SSH İşlemleri (Retry Mekanizmalı)
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
                    await Task.Delay(1000); // 1 saniye bekle
                }
            }

            if (!connected) throw new Exception("VPN sunucusuna erişilemiyor (SSH Timeout).");

            try
            {
                // Script sudo ile şifresiz çalışmalı (visudo ayarı yapılmış olmalı)
                var commandText = $"sudo /etc/wireguard/add_peer.sh {clientName} {clientIp}";
                var cmd = ssh.CreateCommand(commandText);
                var result = cmd.Execute(); // RunCommand yerine CreateCommand daha kontrollüdür

                if (cmd.ExitStatus != 0)
                {
                    _logger.LogError($"Script Hatası: {cmd.Error}");
                    throw new Exception("VPN kullanıcısı oluşturulamadı.");
                }

                // 5. Config İndirme (SFTP)
                using var sftp = new SftpClient(connection);
                sftp.Connect();

                string remotePath = targetServer.SshUser == "root"
                    ? $"/root/{clientName}.conf"
                    : $"/home/{targetServer.SshUser}/{clientName}.conf";

                if (!sftp.Exists(remotePath))
                    throw new Exception("Config dosyası oluşmadı.");

                // Dosya boyut kontrolü (Memory patlamasın)
                var fileAttr = sftp.GetAttributes(remotePath);
                if (fileAttr.Size > 50_000) // 50 KB
                    throw new Exception("Config dosyası anormal derecede büyük.");

                using var ms = new MemoryStream();
                sftp.DownloadFile(remotePath, ms);

                var configText = Encoding.UTF8.GetString(ms.ToArray());
                sftp.Disconnect();
                ssh.Disconnect(); // İşimiz bitti

                // 6. DB Kaydı
                await _userVpnRepo.InsertAsync(new UserVpn
                {
                    UserId = userId,
                    VpnServerId = vpnServerId,
                    ClientIp = clientIp,
                    ConnectedAt = DateTime.UtcNow,
                    IsActive = true,
                    ClientConfig = configText
                });

                _logger.LogInformation($"User {userId} başarıyla {clientIp} IP'si ile bağlandı.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VPN oluşturma sürecinde hata.");
                throw; // Controller yakalasın
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
                return; // Zaten yok, hata vermeye gerek yok
            }

            var vpnServer = activeVpn.VpnServer;

            // Eğer sunucu silinmişse vs. DB'den düşürmemiz lazım, SSH yapamayız.
            if (vpnServer == null)
            {
                activeVpn.IsActive = false;
                await _userVpnRepo.UpdateAsync(activeVpn);
                return;
            }

            try
            {
                // SSH Key ile bağlantı
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
                var command = $"sudo /etc/wireguard/remove_peer.sh {clientName}";

                var cmd = ssh.RunCommand(command);

                // 🔥 KRİTİK: Script hata verse bile (mesela peer zaten yok), 
                // biz DB'den kaydı düşmeliyiz. Kullanıcıyı "Hata oluştu" diye kilitlememeliyiz.
                if (cmd.ExitStatus != 0)
                {
                    _logger.LogWarning($"Peer silinirken uyarı (önemsiz olabilir): {cmd.Error}");
                }

                ssh.Disconnect();
            }
            catch (Exception ex)
            {
                // SSH çalışmasa bile DB'yi güncelle!
                _logger.LogError(ex, "SSH ile silme yapılamadı, ancak DB güncellenecek.");
            }
            finally
            {
                // Her durumda DB'den düşürüyoruz
                activeVpn.IsActive = false;
                await _userVpnRepo.UpdateAsync(activeVpn);
                _logger.LogInformation($"User {userId} bağlantısı sonlandırıldı.");
            }
        }
    }
}