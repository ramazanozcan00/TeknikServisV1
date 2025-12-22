using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TeknikServis.Core.DTOs;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class EvolutionApiWhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly string _instanceName;

        public EvolutionApiWhatsAppService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _instanceName = configuration["EvolutionApi:InstanceName"];
        }

        // Artık temiz bir şekilde bool dönüyor
        public async Task<bool> SendMessageAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(phoneNumber)) return false;

                // Numara temizleme
                string cleanNumber = phoneNumber.Replace(" ", "").Replace("+", "").Replace("-", "").Trim();
                if (cleanNumber.StartsWith("0")) cleanNumber = cleanNumber.Substring(1);
                if (cleanNumber.StartsWith("5")) cleanNumber = "90" + cleanNumber;

                // Yeni sadeleştirilmiş payload
                var payload = new SendMessageRequest
                {
                    number = cleanNumber,
                    text = message,
                    delay = 1200
                };

                var response = await _httpClient.PostAsJsonAsync($"/message/sendText/{_instanceName}", payload);

                // Sadece başarılıysa true dön
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Loglama yapılabilir
                return false;
            }
        }
    }
}