using System.Net;
using System.Net.Mail;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Web.Services
{
    public class SmtpEmailService : IEmailService
    {
        // Veritabanına erişmek için UnitOfWork ekliyoruz (veya IRepository)
        // NOT: Servisler Scoped olduğu için IUnitOfWork inject edilebilir.
        // Ancak IUnitOfWork'ü Service katmanından değil Web katmanından erişim için 
        // doğrudan IServiceProvider ile scope açarak çekmek daha güvenli olabilir 
        // ama burada Dependency Injection ile ilerleyelim.

        private readonly IServiceProvider _serviceProvider;

        public SmtpEmailService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            await SendEmailWithAttachmentAsync(to, subject, body, null, null);
        }

        public async Task SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName)
        {
            // Ayarları veritabanından çek
            EmailSetting settings;

            // Scope oluşturup veritabanına bağlanıyoruz (Best Practice)
            using (var scope = _serviceProvider.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var settingsList = await unitOfWork.Repository<EmailSetting>().GetAllAsync();
                settings = settingsList.FirstOrDefault();
            }

            if (settings == null)
            {
                // Ayar yoksa hata fırlatabilir veya varsayılan/boş dönebilirsiniz
                throw new Exception("Sistem mail ayarları yapılmamış! Lütfen Admin panelinden ayarları giriniz.");
            }

            // Dinamik Ayarları Kullan
            var smtpClient = new SmtpClient(settings.SmtpHost)
            {
                Port = settings.SmtpPort,
                Credentials = new NetworkCredential(settings.SenderEmail, settings.SenderPassword),
                EnableSsl = settings.EnableSsl,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings.SenderEmail, "Teknik Servis"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(to);

            if (attachmentData != null && attachmentData.Length > 0)
            {
                var stream = new MemoryStream(attachmentData);
                var attachment = new Attachment(stream, attachmentName, "application/pdf");
                mailMessage.Attachments.Add(attachment);
            }

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}