using System;
using System.Net;
using System.Net.Mail;

namespace VOID_STORE.Models
{
    public static class EmailManager
    {
        // parametre olarak alinan eposta adresine belirtilen dogrulama kodunu smtp protokolu uzerinden gonder
        public static bool SendVerificationEmail(string toEmail, string code)
        {
            try
            {
                // appsettings dosyasindan sistem eposta konfigurasyonlarini okuyarak degiskene atar
                var config = ConfigManager.GetEmailConfig();
                
                // appsettings icerisindeki eposta yapilandirmasini kontrol et
                if (string.IsNullOrEmpty(config.Email) || string.IsNullOrEmpty(config.Password))
                {
                    // eposta ayarlari eksikse exception firlatarak ust katmana bildir
                    throw new InvalidOperationException("eposta ayarlari eksik mail gonderilemiyor");
                }

                // smtpclient nesnesini ayarlanan adres ve port ile olustur islem sonunda otomatik kapanmasini sagla
                using (SmtpClient smtp = new SmtpClient(config.SmtpAddress, config.SmtpPort))
                {
                    // guvenli baglanti ssl kullanilmasini zorunlu kil
                    smtp.EnableSsl = true;
                    // smtp sunucusuna giris yapabilmek icin gerekli olan eposta ve google uygulama sifresi kimlik dogrulama bilgilerini ata
                    smtp.Credentials = new NetworkCredential(config.Email, config.Password);

                    using (MailMessage mail = new MailMessage())
                    {
                        mail.From = new MailAddress(config.Email, "VOID STORE");
                        mail.To.Add(toEmail);
                        // epostanin konu basligi
                        mail.Subject = "VOID STORE - Hesap Dogrulama Kodu";

                        // kullaniciya gonderilecek olan epostanin html formatindaki gorsel sablonu
                        string body = $@"
                        <div style='font-family: Arial, sans-serif; background-color: #0A0A0C; color: #FFFFFF; padding: 30px; border-radius: 10px; max-width: 500px; margin: auto; text-align: center;'>
                            <img src='https://raw.githubusercontent.com/ht-HAKAN/VOID-STORE/master/VOID-STORE/voidstoreimages/VOIDSTORE_NOBG_LOGO.png' alt='VOID STORE Logo' style='max-width: 250px; margin-bottom: 5px;' />
                            <h2 style='color: #FFFFFF; border-bottom: 1px solid #333; padding-bottom: 15px; margin-top: 0;'>Hoşgeldiniz!</h2>
                            <p style='color: #CCCCCC; font-size: 14px;'>Hesabınızı başarıyla oluşturmak için doğrulama kodunuz aşağıdadır:</p>
                            <div style='background-color: #18181D; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                <span style='font-size: 32px; font-weight: bold; color: #E81123; letter-spacing: 5px;'>{code}</span>
                            </div>
                            <p style='color: #888888; font-size: 12px; text-align: center;'>Bu kod 10 dakika boyunca geçerlidir. Lütfen kimseyle paylaşmayın.</p>
                        </div>";

                        mail.Body = body;
                        // mail iceriginin duz metin degil html kodlari barindirdigini belirt
                        mail.IsBodyHtml = true;

                        // hazirlanan eposta nesnesini smtp sunucusu uzerinden hedefe ilet
                        smtp.Send(mail);
                        // islem sorunsuz tamamlandiginda true degeri dondur
                        return true; 
                    }
                }
            }
            catch (Exception)
            {
                // hatayi ust katmana ilet
                throw;
            }
        }
        // sifre sifirlama maili gonder
        public static bool SendResetCodeEmail(string toEmail, string code)
        {
            try
            {
                var config = ConfigManager.GetEmailConfig();
                
                if (string.IsNullOrEmpty(config.Email) || string.IsNullOrEmpty(config.Password))
                {
                    throw new InvalidOperationException("eposta ayarlari eksik mail gonderilemiyor");
                }

                using (SmtpClient smtp = new SmtpClient(config.SmtpAddress, config.SmtpPort))
                {
                    smtp.EnableSsl = true;
                    smtp.Credentials = new NetworkCredential(config.Email, config.Password);

                    using (MailMessage mail = new MailMessage())
                    {
                        mail.From = new MailAddress(config.Email, "VOID STORE");
                        mail.To.Add(toEmail);
                        mail.Subject = "VOID STORE - Şifre Sıfırlama Kodu";

                        string body = $@"
                        <div style='font-family: Arial, sans-serif; background-color: #0A0A0C; color: #FFFFFF; padding: 30px; border-radius: 10px; max-width: 500px; margin: auto; text-align: center;'>
                            <img src='https://raw.githubusercontent.com/ht-HAKAN/VOID-STORE/master/VOID-STORE/voidstoreimages/VOIDSTORE_NOBG_LOGO.png' alt='VOID STORE Logo' style='max-width: 250px; margin-bottom: 5px;' />
                            <h2 style='color: #FFFFFF; border-bottom: 1px solid #333; padding-bottom: 15px; margin-top: 0;'>Şifre Sıfırlama</h2>
                            <p style='color: #CCCCCC; font-size: 14px;'>Hesabınızın şifresini sıfırlamak için doğrulama kodunuz aşağıdadır:</p>
                            <div style='background-color: #18181D; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                                <span style='font-size: 32px; font-weight: bold; color: #E81123; letter-spacing: 5px;'>{code}</span>
                            </div>
                            <p style='color: #888888; font-size: 12px; text-align: center;'>Bu kod 10 dakika boyunca geçerlidir. Eğer bu işlemi siz talep etmediyseniz, lütfen bu mesaja itibar etmeyiniz.</p>
                        </div>";

                        mail.Body = body;
                        mail.IsBodyHtml = true;

                        smtp.Send(mail);
                        return true; 
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
