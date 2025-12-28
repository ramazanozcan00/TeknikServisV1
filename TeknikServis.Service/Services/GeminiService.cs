using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TeknikServis.Core.Entities;

namespace TeknikServis.Service.Services
{
    public interface IGeminiService
    {
        Task<string> GenerateResponseAsync(string userMessage, ServiceTicket ticket, string customerName);
    }

    public class GeminiService : IGeminiService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public GeminiService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<string> GenerateResponseAsync(string userMessage, ServiceTicket ticket, string customerName)
        {
            var apiKey = _configuration["GoogleGemini:ApiKey"];
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            // DÜZELTME: ticket.DeviceBrand?.Name kullanıldı. (Önceki kodda BrandName yazıyordu)
            var promptContext = $@"
                Sen 'Teknik Servis' firmasının nazik ve yardımsever sanal asistanısın.
                Müşteri İsmi: {customerName}
                
                Müşterinin Cihaz Bilgileri:
                - Cihaz: {ticket.DeviceBrand?.Name ?? "Belirtilmemiş"} {ticket.DeviceModel}
                - Seri No: {ticket.SerialNumber}
                - Arıza Şikayeti: {ticket.ProblemDescription}
                - Mevcut Durum: {ticket.Status}
                - Teknisyen Notu: {ticket.TechnicianNotes ?? "Henüz not girilmedi."}
                - Toplam Ücret: {ticket.TotalPrice:C2}
                - Garanti: {(ticket.IsWarranty ? "Var" : "Yok")}

                Kurallar:
                1. Müşterinin sorusuna yukarıdaki bilgilere göre cevap ver.
                2. Sadece cihazla ilgili soruları cevapla.
                3. Cevabın kısa, net ve Türkçe olsun.
                
                Müşteri Sorusu: {userMessage}";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = promptContext } } }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);

                if (!response.IsSuccessStatusCode) return "Sistem yoğunluğu nedeniyle şu an cevap veremiyorum.";

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("candidates", out var candidates))
                {
                    return candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                }
                return "Cevap üretilemedi.";
            }
            catch
            {
                return "Bağlantı hatası.";
            }
        }
    }
}