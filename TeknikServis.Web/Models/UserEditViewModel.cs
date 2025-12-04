using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class UserEditViewModel
    {
        public string Id { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string Email { get; set; }

        public Guid BranchId { get; set; }
        public string UserRole { get; set; }

        public string? Password { get; set; }

        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        public int PrintBalance { get; set; }
        public int MailBalance { get; set; }
        public int CustomerBalance { get; set; }
        public int TicketBalance { get; set; }

        public bool TwoFactorEnabled { get; set; }

        // BU ALAN MUTLAKA OLMALI:
        public bool IsSidebarVisible { get; set; }

        public List<Guid> SelectedBranchIds { get; set; } = new List<Guid>();

        // Menüler
        public bool ShowHome { get; set; }
        public bool ShowCustomer { get; set; }
        public bool ShowService { get; set; }
        public bool ShowBarcode { get; set; }
        public bool ShowEDevlet { get; set; }
        public bool ShowAudit { get; set; }
        public bool ShowSupport { get; set; }
      
        public bool ShowStock { get; set; }
    }
}