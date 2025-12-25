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
            // Try-catch bloğunu kaldırdık veya sadece loglama için kullanıp hatayı yeniden fırlatacak şekilde düzenledik.
            // Böylece hata Controller'a ulaşır ve kullanıcıya gösterilebilir.

            // 1. Ayarları Veritabanından Çek
            var setting = (await _unitOfWork.Repository<WhatsAppSetting>()
                .FindAsync(x => x.BranchId == branchId)).FirstOrDefault();

            // 2. Temel Kontroller
            if (setting == null) throw new Exception("Bu şube için WhatsApp ayarı bulunamadı.");
            if (!setting.IsActive) throw new Exception("WhatsApp servisi pasif durumda.");
            if (string.IsNullOrEmpty(phoneNumber)) throw new Exception("Telefon numarası boş olamaz.");

            // Kredi Kontrolü
            if (setting.WhatsAppCredit <= 0)
            {
                throw new Exception("Yetersiz WhatsApp kredisi.");
            }

            // 3. Numara Temizleme
            string cleanNumber = phoneNumber.Replace(" ", "").Replace("+", "").Replace("-", "").Trim();
            if (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
            if (cleanNumber.StartsWith("5")) cleanNumber = "90" + cleanNumber;

            // 4. Payload Hazırlama
            var payload = new
            {
                number = cleanNumber,
                text = message,
                delay = 1200,
                linkPreview = false
            };

            // 5. URL Hazırlama
            string requestUrl;
            if (!string.IsNullOrEmpty(setting.ApiUrl))
            {
                var baseUrl = setting.ApiUrl.TrimEnd('/');
                requestUrl = $"{baseUrl}/message/sendText/{setting.InstanceName}";
            }
            else
            {
                // Fallback
                requestUrl = $"message/sendText/{setting.InstanceName}";
            }

            // Request Oluşturma
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            // API Key Yönetimi
            if (!string.IsNullOrEmpty(setting.ApiKey))
            {
                if (request.Headers.Contains("apikey"))
                {
                    request.Headers.Remove("apikey");
                }
                request.Headers.Add("apikey", setting.ApiKey);
            }

            request.Content = JsonContent.Create(payload);

            // 6. Gönderim İşlemi
            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    // Başarılı ise krediyi 1 azalt
                    setting.WhatsAppCredit -= 1;
                    _unitOfWork.Repository<WhatsAppSetting>().Update(setting);
                    await _unitOfWork.CommitAsync();

                    return true;
                }
                else
                {
                    // API'den gelen hata mesajını oku ve fırlat
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"WhatsApp API Hatası ({response.StatusCode}): {errorContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"Sunucuya ulaşılamadı. URL: {requestUrl}. Hata: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Servis Hatası: {ex.Message}");
            }
        }
    }
}