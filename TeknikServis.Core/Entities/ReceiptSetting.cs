using System;

namespace TeknikServis.Core.Entities
{
    public class ReceiptSetting : BaseEntity
    {
        public string LogoPath { get; set; }      // Logo Resim Yolu
        public string HeaderText { get; set; }    // Logo Altındaki Yazı
        public string ServiceTerms { get; set; }  // Servis Şartları (Uzun Metin)
    }
}