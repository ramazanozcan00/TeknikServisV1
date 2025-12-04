namespace TeknikServis.Web.Models
{
    public class ImeiResult
    {
        public bool IsRegistered { get; set; }
        public string StatusMessage { get; set; }
        public string Model { get; set; }
        public string Imei { get; set; }
    }
}