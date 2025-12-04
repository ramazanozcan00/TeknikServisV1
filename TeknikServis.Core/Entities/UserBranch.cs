using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeknikServis.Core.Entities
{
    public class UserBranch:BaseEntity
    {
        public Guid UserId { get; set; }
        public virtual AppUser AppUser { get; set; }

        public Guid BranchId { get; set; }
        public virtual Branch Branch { get; set; }
    }
}