using BusiniessLayer.Abstract;
using DataAcsessLayer.Abstract;
using EntityLayer.Concrete;
using Renci.SshNet;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BusiniessLayer.Concrete
{
    public class UserVpnManager : IUserVpnService
    {
        private readonly IUserVpnDal _userVpnRepo;
        private readonly IVpnServerDal _vpnServerRepo;

        public UserVpnManager(IUserVpnDal userVpnRepo, IVpnServerDal vpnServerRepo)
        {
            _userVpnRepo = userVpnRepo;
            _vpnServerRepo = vpnServerRepo;
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

        // ✅ GÜNCELLENDİ: Senin sunucunun IP bloğuna (10.66.66.x) göre ayarlandı
        private async Task<string> GetNextFreeIpAsync(int vpnServerId)
        {
            var existingVpns = await _userVpnRepo.GetAllFilterAsync(x => x.VpnServerId == vpnServerId);

            if (!existingVpns.Any())
            {
                // İlk kullanıcı 10.66.66.2 alacak (1 numara sunucuda)
                return "10.66.66.2";
            }

            var maxIpOctet = existingVpns
                .Where(x => !string.IsNullOrEmpty(x.ClientIp))
                .Select(x => int.Parse(x.ClientIp.Split('.')[3]))
                .Max();

            if (maxIpOctet >= 253) throw new Exception("IP Havuzu Doldu!");

            return $"10.66.66.{maxIpOctet + 1}";
        }

        public async Task ConnectUserToVpnAsync(Guid userId, int vpnServerId)
        {
            // 1️⃣ Sunucuyu Çek
            var targetServer = await _vpnServerRepo.GetByIdAsync(vpnServerId);
            if (targetServer == null) throw new Exception("Sunucu bulunamadı!");

            // 2️⃣ IP ve İsim Belirle
            var clientName = $"user_{userId.ToString().Substring(0, 8)}";
            var clientIp = await GetNextFreeIpAsync(vpnServerId);

            // 3️⃣ Bağlantı Bilgileri (Şifre artık DB'den geliyor, Entity'de SshPassword alanı olmalı!)
            // Eğer Entity'de yoksa buraya geçici olarak "Sifreniz" yazabilirsin ama doğrusu bu.
            var auth = new PasswordAuthenticationMethod(targetServer.SshUser, targetServer.SshPassword ?? "123");

            var connection = new ConnectionInfo(
                targetServer.IpAddress,
                targetServer.SshPort,
                targetServer.SshUser,
                auth
            );

            // 4️⃣ SSH İşlemleri (Kullanıcı Oluşturma)
            using var ssh = new SshClient(connection);
            ssh.Connect();

            // Scriptin 443 portuyla ayarlı olduğundan emin ol (önceki konuşmamızdaki gibi)
            var command = $"sudo /etc/wireguard/add_peer.sh {clientName} {clientIp}";
            var result = ssh.RunCommand(command);

            if (result.ExitStatus != 0 && !result.Result.Contains("OK"))
                throw new Exception("VPN peer oluşturulamadı: " + result.Error);

            ssh.Disconnect();

            // 5️⃣ Config Dosyasını Çek (SFTP)
            using var sftp = new SftpClient(connection);
            sftp.Connect();

            using var ms = new MemoryStream();

            // 🔥 KRİTİK DÜZELTME: Root kullanıcısı için yol farklıdır
            string remotePath;
            if (targetServer.SshUser == "root")
            {
                remotePath = $"/root/{clientName}.conf";
            }
            else
            {
                remotePath = $"/home/{targetServer.SshUser}/{clientName}.conf";
            }

            // Dosya kontrolü (Opsiyonel güvenlik)
            if (!sftp.Exists(remotePath))
            {
                sftp.Disconnect();
                throw new Exception($"Config dosyası sunucuda oluşmadı: {remotePath}");
            }

            sftp.DownloadFile(remotePath, ms);

            var configText = Encoding.UTF8.GetString(ms.ToArray());
            sftp.Disconnect();

            // 6️⃣ DB Kaydı
            await _userVpnRepo.InsertAsync(new UserVpn
            {
                UserId = userId,
                VpnServerId = vpnServerId,
                ClientIp = clientIp,
                ConnectedAt = DateTime.UtcNow,
                IsActive = true,
                ClientConfig = configText
            });
        }

        public async Task DisconnectAsync(Guid userId)
        {
            // 1. Aktif bağlantıyı bul
            var activeVpn = await _userVpnRepo.GetByFilterAsync(
                x => x.UserId == userId && x.IsActive,
                x => x.VpnServer
            );

            if (activeVpn == null)
                throw new Exception("Aktif VPN bağlantısı bulunamadı.");

            var vpnServer = activeVpn.VpnServer ?? throw new Exception("VPN Server bilgisine erişilemedi!");

            // 2. SSH Bağlantısı (Şifre DB'den)
            var auth = new PasswordAuthenticationMethod(vpnServer.SshUser, vpnServer.SshPassword ?? "123");
            var connection = new ConnectionInfo(
                vpnServer.IpAddress,
                vpnServer.SshPort,
                vpnServer.SshUser,
                auth
            );

            using var ssh = new SshClient(connection);
            ssh.Connect();

            // 3. Kullanıcı adını belirle
            var clientName = $"user_{userId.ToString().Substring(0, 8)}";

            // 4. Silme Komutu
            var command = $"sudo /etc/wireguard/remove_peer.sh {clientName}";

            var result = ssh.RunCommand(command);

            // Silmede hata olsa bile DB'den düşürelim mi? Genelde hayır, hata fırlatmak daha güvenli.
            if (!string.IsNullOrEmpty(result.Error) && !result.Result.Contains("OK"))
                throw new Exception("VPN peer silinemedi: " + result.Error);

            ssh.Disconnect();

            // 5. DB'de Pasife Çek
            activeVpn.IsActive = false;
            await _userVpnRepo.UpdateAsync(activeVpn);
        }
    }
}