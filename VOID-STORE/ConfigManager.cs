using System;
using System.IO;
using System.Text.Json;

namespace VOID_STORE
{
    // JSON yapılandırma dosyasındaki e-posta model sınıfı
    public class EmailConfig
    {
        public string SmtpAddress { get; set; }
        public int SmtpPort { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // Genel uygulama yapılandırması model sınıfı
    public class AppConfig
    {
        public EmailConfig EmailSettings { get; set; }
    }

    public static class ConfigManager
    {
        // JSON dosyasından e-posta konfigürasyonlarını içeren bölümü oku ve EmailConfig nesnesi olarak döndür.
        public static EmailConfig GetEmailConfig()
        {
            try
            {
                // appsettings.json dosyasının proje dizininde var olup olmadığını kontrol et.
                if (File.Exists("appsettings.json"))
                {
                    // Dosyanın tüm içeriğini metin formatında oku.
                    string jsonString = File.ReadAllText("appsettings.json");
                    // Okunan metin formatındaki JSON dosyasını C# tarafındaki AppConfig nesnesine çevir.
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonString);
                    // Ayarlar mevcutsa döndür, değilse boş bir EmailConfig nesnesi oluştur.
                    return config?.EmailSettings ?? new EmailConfig();
                }
                else
                {
                    // Dosya bulunamadığı takdirde kullanıcıya hata göster.
                    CustomError.ShowDialog("appsettings.json dosyası bulunamadı!", "SİSTEM HATASI");
                    return new EmailConfig();
                }
            }
            catch (Exception ex)
            {
                CustomError.ShowDialog("Ayarlar dosyası okunamadı: " + ex.Message, "SİSTEM HATASI");
                return new EmailConfig();
            }
        }
    }
}
