using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Services;

namespace TeknikServis.Service.Services
{
    public class IletiMerkeziSmsService : ISmsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;

        public IletiMerkeziSmsService(IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory)
        {
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<SmsResult> SendSmsAsync(string telefon, string mesaj)
        {
            try
            {
                // 1. Aktif Ayarı Veritabanından Çek
                var settings = await _unitOfWork.Repository<SmsSetting>().GetAllAsync();
                var config = settings.FirstOrDefault(); // Genelde tek kayıt olur

                if (config == null || !config.IsActive)
                {
                    return SmsResult.Failure("SMS Ayarları yapılandırılmamış veya aktif değil.");
                }

                // 2. XML Oluştur (Dinamik Verilerle)
                // Not: İleti Merkezi XML formatı örnektir, sağlayıcınıza göre değişebilir.
                string xmlData = $@"
                    <request>
                        <authentication>
                            <username>{config.ApiUsername}</username>
                            <password>{config.ApiPassword}</password>
                        </authentication>
                        <order>
                            <sender>{config.SmsTitle}</sender>
                            <sendDateTime></sendDateTime>
                            <message>
                                <text><![CDATA[{mesaj}]]></text>
                                <receipents>
                                    <number>{telefon}</number>
                                </receipents>
                            </message>
                        </order>
                    </request>";

                // 3. İsteği Gönder
                var client = _httpClientFactory.CreateClient();
                var content = new StringContent(xmlData, Encoding.UTF8, "text/xml");
                var response = await client.PostAsync(config.ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // Basit kontrol: status code 200 ise başarılı varsayıyoruz. 
                    // Detaylı XML parse yapılabilir.
                    return SmsResult.Success();
                }

                return SmsResult.Failure($"API Hatası: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return SmsResult.Failure($"Hata: {ex.Message}");
            }
        }
    }
}