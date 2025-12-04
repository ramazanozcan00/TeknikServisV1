using System.Threading.Tasks;

namespace TeknikServis.Web.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName);
    }
}
