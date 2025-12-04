
using System.Threading.Tasks;

namespace TeknikServis.Web.Services
{
    using System.Threading.Tasks;

    namespace TeknikServis.Web.Services
    {
        public class SmsResult
        {
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; }

            public static SmsResult Success() => new SmsResult { IsSuccess = true };
            public static SmsResult Failure(string message) => new SmsResult { IsSuccess = false, ErrorMessage = message };
        }

        public interface ISmsService
        {
            Task<SmsResult> SendSmsAsync(string telefon, string mesaj);
        }
    }
}