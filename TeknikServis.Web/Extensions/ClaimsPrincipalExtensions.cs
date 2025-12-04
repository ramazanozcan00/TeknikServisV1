using System.Security.Claims;

namespace TeknikServis.Web.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetBranchId(this ClaimsPrincipal principal)
        {
            var branchIdValue = principal.FindFirst("BranchId")?.Value;
            if (Guid.TryParse(branchIdValue, out Guid branchId)) return branchId;
            return Guid.Empty;
        }

        public static string GetFullName(this ClaimsPrincipal principal)
        {
            return principal.FindFirst("FullName")?.Value ?? "";
        }

        // --- YENİ EKLENEN METOT ---
        public static string GetBranchName(this ClaimsPrincipal principal)
        {
            return principal.FindFirst("BranchName")?.Value ?? "Şube Yok";
        }
    }
}