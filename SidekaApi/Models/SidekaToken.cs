using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("SdTokens")]
    public class SidekaToken
    {
        [Key]
        public string Token { get; set; }

        public int UserId { get; set; }
        public int DesaId { get; set; }
        public string Info { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
