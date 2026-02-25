using BusiniessLayer.Abstract;
using EntityLayer.Concrete; // 🔥 GÜNCELLEME: VpnProtocol'ü tanıması için eklendi
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System;
using System.Threading.Tasks;

namespace VpnWeb.Controllers
{
    [Authorize]
    public class ConnectController : Controller
    {
        private readonly IVpnServerService _vpnService;
        private readonly IUserVpnService _userVpnService;

        public ConnectController(
            IVpnServerService vpnService,
            IUserVpnService userVpnService)
        {
            _vpnService = vpnService;
            _userVpnService = userVpnService;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

        public async Task<IActionResult> Index()
        {
            bool hasActive = await _userVpnService.HasActiveVpnAsync(CurrentUserId);

            if (hasActive)
            {
                return RedirectToAction("Status");
            }

            var vpns = _vpnService.GetActiveServers();
            return View(vpns);
        }

        // 🔥 GÜNCELLEME: VpnProtocol parametresi eklendi
        public async Task<IActionResult> Connect(int id, VpnProtocol protocol)
        {
            try
            {
                // Protokolü manager'a gönderiyoruz
                await _userVpnService.ConnectUserToVpnAsync(CurrentUserId, id, protocol);
                return RedirectToAction("Status");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Status()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            var activeVpn = await _userVpnService.GetActiveVpnAsync(userId);

            if (activeVpn == null)
            {
                ViewBag.Message = "Aktif bir VPN bağlantınız yok.";
                return View(null);
            }

            // 🔥 GÜNCELLEME: Sadece WireGuard için QR kod üret, OpenVPN'i atla
            if (!string.IsNullOrEmpty(activeVpn.ClientConfig) && activeVpn.Protocol == VpnProtocol.WireGuard)
            {
                try
                {
                    using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                    {
                        QRCodeData qrCodeData = qrGenerator.CreateQrCode(activeVpn.ClientConfig, QRCodeGenerator.ECCLevel.Q);
                        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                        byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
                        string base64String = Convert.ToBase64String(qrCodeAsPngByteArr);
                        ViewBag.QrCodeImage = "data:image/png;base64," + base64String;
                    }
                }
                catch (Exception ex)
                {
                    // Olası bir boyut aşımında sayfa çökmesin diye log veya viewbag atıyoruz
                    ViewBag.QrCodeError = "QR kod oluşturulamadı (Veri çok büyük).";
                }
            }

            return View(activeVpn);
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                await _userVpnService.DisconnectAsync(CurrentUserId);
                TempData["Success"] = "VPN bağlantısı kapatıldı";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Status");
        }

        [Authorize]
        public async Task<IActionResult> DownloadConfig()
        {
            var vpn = await _userVpnService.GetActiveVpnAsync(CurrentUserId);

            if (vpn == null || string.IsNullOrEmpty(vpn.ClientConfig))
            {
                TempData["Error"] = "İndirilecek aktif VPN config bulunamadı";
                return RedirectToAction("Status");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(vpn.ClientConfig);

            // 🔥 GÜNCELLEME: Dosya uzantısını ve adını protokole göre belirliyoruz
            string extension = vpn.Protocol == VpnProtocol.OpenVPN ? ".ovpn" : ".conf";
            string prefix = vpn.Protocol == VpnProtocol.OpenVPN ? "ovpn" : "wg";

            var fileName = $"{prefix}-{CurrentUserId.ToString().Substring(0, 6)}{extension}";

            return File(
                bytes,
                "application/octet-stream",
                fileName
            );
        }
    }
}