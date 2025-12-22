namespace TeknikServis.Core.DTOs
{
    public class SendMessageRequest
    {
        public string number { get; set; } // Telefon
        public string text { get; set; }   // Mesaj (Artık direkt burada)
        public int delay { get; set; } = 1200; // Gecikme
    }

    public class TextMessage
    {
        public string text { get; set; }
    }

    public class Options
    {
        public int delay { get; set; } = 1200;
        public string presence { get; set; } = "composing";
    }
}