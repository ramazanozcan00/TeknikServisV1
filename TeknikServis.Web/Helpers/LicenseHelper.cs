using System;
using System.Security.Cryptography;
using System.Text;

namespace TeknikServis.Web.Helpers
{
    public static class LicenseHelper
    {
        private static readonly string SecretKey = "TeknikServis_Secure_Key_2024!"; // Gizli anahtarınız

        // Anahtar Üretme: BranchId | BitişTarihi | Hash
        public static string GenerateKey(Guid branchId, DateTime date)
        {
            string rawData = $"{branchId}|{date:yyyy-MM-dd}";
            string hash = ComputeHash(rawData + SecretKey);

            // Veriyi Base64 ile şifrele
            byte[] bytes = Encoding.UTF8.GetBytes($"{rawData}|{hash}");
            return Convert.ToBase64String(bytes);
        }

        // Anahtar Doğrulama
        public static bool ValidateKey(string key, Guid branchId, out DateTime date)
        {
            date = DateTime.MinValue;
            try
            {
                // Çözümleme
                byte[] bytes = Convert.FromBase64String(key);
                string decoded = Encoding.UTF8.GetString(bytes);
                string[] parts = decoded.Split('|');

                if (parts.Length != 3) return false;

                string bId = parts[0];
                string dStr = parts[1];
                string hash = parts[2];

                // 1. Şube Kontrolü (Başka şubenin anahtarını kullanamaz)
                if (bId != branchId.ToString()) return false;

                // 2. Hash Kontrolü (Tarihle oynanmış mı?)
                string reHash = ComputeHash($"{bId}|{dStr}" + SecretKey);
                if (hash != reHash) return false;

                date = DateTime.Parse(dStr);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeHash(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}