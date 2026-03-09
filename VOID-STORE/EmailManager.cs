using System;
using System.Net;
using System.Net.Mail;

namespace VOID_STORE
{
    public static class EmailManager
    {
        // Parametre olarak alınan e-posta adresine, belirtilen doğrulama kodunu SMTP protokolü üzerinden gönder.
        public static bool SendVerificationEmail(string toEmail, string code)
        {
            try
            {
                // Appsettings dosyasından sistem e-posta konfigürasyonlarını okuyarak değişkene atar.
                var config = ConfigManager.GetEmailConfig();
                
                // appsettings içerisindeki e-posta yapılandırmasını kontrol et.
                if (string.IsNullOrEmpty(config.Email) || string.IsNullOrEmpty(config.Password))
                {
                    CustomError.ShowDialog("E-posta ayarları eksik. Mail gönderilemiyor.", "SİSTEM HATASI");
                    return false;
                }

                // SmtpClient nesnesini ayarlanan Adres ve Port ile oluştur. İşlem sonunda otomatik kapanmasını sağla.
                using (SmtpClient smtp = new SmtpClient(config.SmtpAddress, config.SmtpPort))
                {
                    // Güvenli bağlantı (SSL) kullanılmasını zorunlu kıl.
                    smtp.EnableSsl = true;
                    // SMTP sunucusuna giriş yapabilmek için gerekli olan e-posta ve Google Uygulama Şifresi kimlik doğrulama bilgilerini ata.
                    smtp.Credentials = new NetworkCredential(config.Email, config.Password);

                    using (MailMessage mail = new MailMessage())
                    {
                        mail.From = new MailAddress(config.Email, "VOID STORE");
                        mail.To.Add(toEmail);
                        // E-postanın konu başlığı
                        mail.Subject = "VOID STORE - Hesap Doğrulama Kodu";

                        // Kullanıcıya gönderilecek olan e-postanın HTML formatındaki görsel şablonu
                        string body = $@"
                        <div style='font-family: Arial, sans-serif; background-color: #0A0A0C; color: #FFFFFF; padding: 30px; border-radius: 10px; max-width: 500px; margin: auto;'>
                            <h2 style='text-align: center; color: #FFFFFF; border-bottom: 1px solid #333; padding-bottom: 10px;'>VOID STORE'a Hoşgeldiniz!</h2>
                            <p style='color: #CCCCCC; font-size: 14px;'>Hesabınızı başarıyla oluşturmak için doğrulama kodunuz aşağıdadır:</p>
                            <div style='background-color: #18181D; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                <span style='font-size: 32px; font-weight: bold; color: #E81123; letter-spacing: 5px;'>{code}</span>
                            </div>
                            <p style='color: #888888; font-size: 12px; text-align: center;'>Bu kod 10 dakika boyunca geçerlidir. Lütfen kimseyle paylaşmayın.</p>
                        </div>";

                        mail.Body = body;
                        // Mail içeriğinin düz metin değil, HTML kodları barındırdığını belirt.
                        mail.IsBodyHtml = true;

                        // Hazırlanan e-posta nesnesini SMTP sunucusu üzerinden hedefe ilet.
                        smtp.Send(mail);
                        // İşlem sorunsuz tamamlandığında true değeri döndür.
                        return true; 
                    }
                }
            }
            catch (Exception ex)
            {
                // Gönderim sırasında oluşabilecek ağ hatalarını yakala ve ekranda göster.
                CustomError.ShowDialog("E-posta gönderilirken bir hata oluştu: " + ex.Message, "E-POSTA HATASI");
                // Hata durumunda false dön.
                return false;
            }
        }
    }
}
