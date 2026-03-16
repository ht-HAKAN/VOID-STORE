using System;
using System.IO;
using System.Text.Json;

namespace VOID_STORE.Models
{
    // json yapilandirma dosyasindaki eposta model sinifi
    public class EmailConfig
    {
        public string SmtpAddress { get; set; }
        public int SmtpPort { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // genel uygulama yapilandirmasi model sinifi
    public class AppConfig
    {
        public EmailConfig EmailSettings { get; set; }
    }

    public static class ConfigManager
    {
        // json dosyasindan eposta konfigurasyonlarini iceren bolumu oku ve emailconfig nesnesi olarak dondur
        public static EmailConfig GetEmailConfig()
        {
            try
            {
                // appsettings.json dosyasinin proje dizininde var olup olmadigini kontrol et
                if (File.Exists("appsettings.json"))
                {
                    // dosyanin tum icerigini metin formatinda oku
                    string jsonString = File.ReadAllText("appsettings.json");
                    // okunan metin formatindaki json dosyasini c# tarafindaki appconfig nesnesine cevir
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonString);
                    // ayarlar mevcutsa dondur degilse bos bir emailconfig nesnesi olustur
                    return config?.EmailSettings ?? new EmailConfig();
                }
                else
                {
                    // dosya bulunamadigi takdirde exception firlatarak ust katmana hatayi ilet
                    throw new FileNotFoundException("appsettings.json dosyasi bulunamadi");
                }
            }
            catch (Exception)
            {
                
                throw;
            }
        }
    }
}
