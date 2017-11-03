using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("WpUsermeta")]
    public class WordpressUserMeta
    {
        [Key]
        public int UmetaId { get; set; }
        public string MetaKey { get; set; }
        public string MetaValue { get; set; }
        public int UserId { get; set; }
    }
}
