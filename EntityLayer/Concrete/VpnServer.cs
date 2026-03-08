using System;
using System.ComponentModel.DataAnnotations;

namespace EntityLayer.Concrete
{
    public class VpnServer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sunucu adı gereklidir")]
        [Display(Name = "Sunucu Adı")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Ülke gereklidir")]
        [Display(Name = "Ülke")]
        public string Country { get; set; }

        [Required(ErrorMessage = "IP adresi gereklidir")]
        [Display(Name = "IP Adresi")]
        public string IpAddress { get; set; }

        [Required(ErrorMessage = "SSH portu gereklidir")]
        [Display(Name = "SSH Port")]
        public int SshPort { get; set; }

        [Required(ErrorMessage = "SSH kullanıcı adı gereklidir")]
        [Display(Name = "SSH Kullanıcı")]
        public string SshUser { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PrivateKeyPath { get; set; }

        public int? LatencyMs { get; set; }
        public int? LoadPercent { get; set; }
    }
}
