using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TeknikServis.Web.Services.TeknikServis.Web.Services;

namespace TeknikServis.Web.Services
{
    public class IletiMerkeziSmsService : ISmsService
    {
        private readonly IConfiguration _configuration;

        public IletiMerkeziSmsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendSmsAsync(string telefon, string mesaj)
        {
            if (string.IsNullOrEmpty(telefon)) return false;

            try
            {
                var username = _configuration["SmsSettings:Username"];
                var password = _configuration["SmsSettings:Password"];
                var senderTitle = _configuration["SmsSettings:SenderTitle"];

                // Telefon numarasını temizle (boşlukları sil, başındaki 0'ı kaldır, +90'ı kaldır)
                telefon = telefon.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
                if (telefon.StartsWith("+90")) telefon = telefon.Substring(3);
                if (telefon.StartsWith("0")) telefon = telefon.Substring(1);

                // XML Formatı
                string xmlData = $@"
                <request>
                    <authentication>
                        <username>{username}</username>
                        <password>{password}</password>
                    </authentication>
                    <order>
                        <sender>{senderTitle}</sender>
                        <sendDateTime></sendDateTime>
                        <message>
                            <text><![CDATA[{mesaj}]]></text>
                            <receipents>
                                <number>{telefon}</number>
                            </receipents>
                        </message>
                    </order>
                </request>";

                using (var client = new HttpClient())
                {
                    // Ileti Merkezi API Endpoint
                    var content = new StringContent(xmlData, Encoding.UTF8, "text/xml");
                    var response = await client.PostAsync("https://api.iletimerkezi.com/v1/send-sms", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        // <status><code>200</code>... başarılıdır.
                        return responseString.Contains("<code>200</code>");
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        Task<SmsResult> ISmsService.SendSmsAsync(string telefon, string mesaj)
        {
            throw new NotImplementedException();
        }
    }
}