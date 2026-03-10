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
        private readonly ILogger<ConnectController> _logger;

        public ConnectController(
            IVpnServerService vpnService,
            IUserVpnService userVpnService,
            ILogger<ConnectController> logger)
        {
            _vpnService = vpnService;
            _userVpnService = userVpnService;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

        public async Task<IActionResult> Index()
        {
            bool hasActive = await _userVpnService.HasActiveVpnAsync(CurrentUserId);

            if (hasActive)
            {
                return RedirectToAction("Status");
            }

            var vpns = await _vpnService.GetActiveServersAsync();
            return View(vpns);
        }

        // 🔥 GÜNCELLEME: VpnProtocol parametresi eklendi
        public async Task<IActionResult> Connect(int id, VpnProtocol protocol)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userId = CurrentUserId;
            try
            {
                await _userVpnService.ConnectUserToVpnAsync(userId, id, protocol);
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
            var userId = CurrentUserId;
            try
            {
                await _userVpnService.DisconnectAsync(userId);
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
            var userId = CurrentUserId;
            var vpn = await _userVpnService.GetActiveVpnAsync(userId);

            if (vpn == null || string.IsNullOrEmpty(vpn.ClientConfig))
            {
                TempData["Error"] = "İndirilecek aktif VPN config bulunamadı";
                return RedirectToAction("Status");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(vpn.ClientConfig);

            string extension = vpn.Protocol == VpnProtocol.OpenVPN ? ".ovpn" : ".conf";
            string prefix = vpn.Protocol == VpnProtocol.OpenVPN ? "ovpn" : "wg";

            var fileName = $"{prefix}-{userId.ToString().Substring(0, 6)}{extension}";

            return File(
                bytes,
                "application/octet-stream",
                fileName
            );
        }
    }
}