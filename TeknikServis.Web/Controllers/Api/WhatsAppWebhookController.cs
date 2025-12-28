using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Interfaces;
using TeknikServis.Service.Services;
using System.Text.Json; // JsonElement için

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly IServiceTicketService _ticketService;
        private readonly ICustomerService _customerService;
        private readonly IGeminiService _geminiService;
        private readonly IWhatsAppService _whatsAppService;

        public WhatsAppWebhookController(
            IServiceTicketService ticketService,
            ICustomerService customerService,
            IGeminiService geminiService,
            IWhatsAppService whatsAppService)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _geminiService = geminiService;
            _whatsAppService = whatsAppService;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveMessage([FromBody] object payload)
        {
            try
            {
                // Gelen veriyi JsonElement'e çeviriyoruz (dynamic yerine güvenli tip)
                var jsonString = JsonSerializer.Serialize(payload);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                // 1. Olay Tipi Kontrolü
                if (root.TryGetProperty("event", out var eventType) && eventType.GetString() != "messages.upsert")
                    return Ok();

                var data = root.GetProperty("data");
                var key = data.GetProperty("key");

                // Kendi attığımız mesajları yoksay
                if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.GetBoolean())
                    return Ok();

                // 2. Mesaj Metnini Al
                var messageData = data.GetProperty("message");
                string userMessage = "";

                if (messageData.TryGetProperty("conversation", out var conversation))
                    userMessage = conversation.GetString();
                else if (messageData.TryGetProperty("extendedTextMessage", out var extended) && extended.TryGetProperty("text", out var text))
                    userMessage = text.GetString();

                if (string.IsNullOrEmpty(userMessage)) return Ok();

                // 3. Telefon Numarasını Al (90555... -> 555...)
                string remoteJid = key.GetProperty("remoteJid").GetString();
                string rawNumber = remoteJid.Split('@')[0];

                string searchNumber = rawNumber;
                if (searchNumber.StartsWith("90")) searchNumber = searchNumber.Substring(2);
                if (searchNumber.StartsWith("0")) searchNumber = searchNumber.Substring(1);

                // 4. Müşteriyi Bul (Eklediğimiz Metot)
                var customer = await _customerService.GetByPhoneAsync(searchNumber);

                if (customer == null) return Ok(); // Müşteri yoksa cevap verme

                // 5. Aktif Servis Fişini Bul (Eklediğimiz Metot)
                var activeTickets = await _ticketService.GetActiveTicketsByCustomerIdAsync(customer.Id);
                var lastTicket = activeTickets.FirstOrDefault(); // En sonuncuyu al

                string responseText;
                if (lastTicket != null)
                {
                    // Gemini Servisine entity özelliklerini senin projene uygun gönderiyoruz
                    responseText = await _geminiService.GenerateResponseAsync(userMessage, lastTicket, customer.FirstName + " " + customer.LastName);
                }
                else
                {
                    responseText = $"Merhaba {customer.FirstName}, şu anda açık bir arıza kaydınız bulunmamaktadır. Yeni işlem için şubemizi ziyaret edebilirsiniz.";
                }

                // 6. Mesajı Gönder (BranchId Eklendi!)
                // Müşterinin kayıtlı olduğu şubeden mesaj atılsın
                await _whatsAppService.SendMessageAsync(rawNumber, responseText, customer.BranchId);

                return Ok();
            }
            catch (Exception ex)
            {
                // Hata loglanabilir
                return Ok();
            }
        }
    }
}