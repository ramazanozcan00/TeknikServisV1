using TeknikServis.Web.Models;

namespace TeknikServis.Web.Services
{
    public interface IEDevletService
    {
        Task<ImeiResult> CheckImeiAsync(string imei);
    }

    public class EDevletService : IEDevletService
    {
        public async Task<ImeiResult> CheckImeiAsync(string imei)
        {
            // --- BURASI SİMÜLASYON ALANIDIR ---
            // Gerçek bir API olmadığı için yapay bir bekleme ve mantık kuruyoruz.
            await Task.Delay(1000);

            var result = new ImeiResult { Imei = imei };

            // Basit Doğrulama: 15 hane mi?
            if (string.IsNullOrEmpty(imei) || imei.Length != 15 || !long.TryParse(imei, out _))
            {
                result.IsRegistered = false;
                result.StatusMessage = "Geçersiz IMEI formatı. 15 haneli sayı girmelisiniz.";
                result.Model = "-";
                return result;
            }

            // TEST MANTIĞI: 
            // Son hanesi ÇİFT sayı ise "Kayıtlı", TEK sayı ise "Kayıt Dışı" diyelim.
            int lastDigit = int.Parse(imei.Last().ToString());

            if (lastDigit % 2 == 0)
            {
                result.IsRegistered = true;
                result.StatusMessage = "IMEI Numarası Kayıtlı.";
                result.Model = "Örnek Marka / Model (İthalat Yoluyla Kaydedilen)";
            }
            else
            {
                result.IsRegistered = false;
                result.StatusMessage = "Kayıt Dışı (Tespit Edilemedi)";
                result.Model = "Bilinmiyor";
            }

            return result;
        }
    }
}