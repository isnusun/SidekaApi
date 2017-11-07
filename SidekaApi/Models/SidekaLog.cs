using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("SdLogs")]
    public class SidekaLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int DesaId { get; set; }
        public DateTime DateAccessed { get; set; }
        public string Token { get; set; }
        public string Action { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Version { get; set; }
        public string Ip { get; set; }
        public string Platform { get; set; }
    }
}
