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
                // 1. Veritabanından o şubenin ayarlarını çek
                var setting = (await _unitOfWork.Repository<WhatsAppSetting>()
                    .FindAsync(x => x.BranchId == branchId)).FirstOrDefault();

                // Ayar yoksa veya pasifse gönderme
                if (setting == null || !setting.IsActive) return false;
                if (string.IsNullOrEmpty(phoneNumber)) return false;

                // 2. Numara Temizleme
                string cleanNumber = phoneNumber.Replace(" ", "").Replace("+", "").Replace("-", "").Trim();
                if (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
                if (cleanNumber.StartsWith("5")) cleanNumber = "90" + cleanNumber;

                // 3. Payload Oluşturma
                var payload = new
                {
                    number = cleanNumber,
                    text = message,
                    delay = 1200,
                    linkPreview = false
                };

                // 4. URL Oluşturma (Sonundaki / işaretini temizleyerek birleştir)
                var baseUrl = setting.ApiUrl.TrimEnd('/');
                var requestUrl = $"{baseUrl}/message/sendText/{setting.InstanceName}";

                // 5. Request Oluşturma (Header işlemleri için HttpRequestMessage kullanıyoruz)
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                // Eğer API Key varsa Header'a ekle
                if (!string.IsNullOrEmpty(setting.ApiKey))
                {
                    request.Headers.Add("apikey", setting.ApiKey);
                }

                request.Content = JsonContent.Create(payload);

                // 6. Gönderim
                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Hata durumunda loglama yapılabilir
                return false;
            }
        }
    }
}