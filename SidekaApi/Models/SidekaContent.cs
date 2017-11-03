using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("SdContents")]
    public class SidekaContent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int DesaId { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Content { get; set; }
        public Int64 Timestamp { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedBy { get; set; }
        public DateTime OpendataDatePushed {get;set;}
        public string OpendataPushError { get; set; }
        public int ChangeId { get; set; }
        public string ApiVersion { get; set; }
    }
}
