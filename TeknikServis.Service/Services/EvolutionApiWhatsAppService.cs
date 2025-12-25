using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TeknikServis.Core.DTOs;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class EvolutionApiWhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IUnitOfWork _unitOfWork;

        public EvolutionApiWhatsAppService(HttpClient httpClient, IUnitOfWork unitOfWork)
        {
            _httpClient = httpClient;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> SendMessageAsync(string phoneNumber, string message, Guid branchId)
        {
            try
            {
                // 1. Ayarları Çek
                var setting = (await _unitOfWork.Repository<WhatsAppSetting>()
                    .FindAsync(x => x.BranchId == branchId)).FirstOrDefault();

                // 2. Kontroller (Ayar var mı, Aktif mi, Numara var mı?)
                if (setting == null || !setting.IsActive) return false;
                if (string.IsNullOrEmpty(phoneNumber)) return false;

                // --- YENİ KREDİ KONTROLÜ ---
                // Kredi 0 veya daha az ise gönderme
                if (setting.WhatsAppCredit <= 0)
                {
                    // İsterseniz buraya log atabilirsiniz: "Kredi yetersiz"
                    return false;
                }
                // ---------------------------

                // 3. Numara Temizleme
                string cleanNumber = phoneNumber.Replace(" ", "").Replace("+", "").Replace("-", "").Trim();
                if (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
                if (cleanNumber.StartsWith("5")) cleanNumber = "90" + cleanNumber;

                // 4. Payload ve Request (Mevcut kodlar)
                var payload = new
                {
                    number = cleanNumber,
                    text = message,
                    delay = 1200,
                    linkPreview = false
                };

                var baseUrl = setting.ApiUrl.TrimEnd('/');
                var requestUrl = $"{baseUrl}/message/sendText/{setting.InstanceName}";

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                if (!string.IsNullOrEmpty(setting.ApiKey))
                {
                    request.Headers.Add("apikey", setting.ApiKey);
                }
                request.Content = JsonContent.Create(payload);

                // 5. Gönderim
                var response = await _httpClient.SendAsync(request);

                // --- YENİ KREDİ DÜŞME İŞLEMİ ---
                if (response.IsSuccessStatusCode)
                {
                    // Başarılı ise krediyi 1 azalt
                    setting.WhatsAppCredit -= 1;

                    // Güncellemeyi kaydet
                    _unitOfWork.Repository<WhatsAppSetting>().Update(setting);
                    await _unitOfWork.CommitAsync();

                    return true;
                }
                // -------------------------------

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}