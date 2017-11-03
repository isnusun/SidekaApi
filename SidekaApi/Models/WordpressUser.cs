using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("WpUsers")]
    public class WordpressUser
    {
        [Key]
        public int ID { get; set; }

        public string UserLogin { get; set; }
        public string UserPass { get; set; }
        public string UserNicename { get; set; }
        public string UserEmail { get; set; }
        public string UserUrl { get; set; }
        public DateTime UserRegistered { get; set; }
        public string UserActivationKey { get; set; }
        public int UserStatus { get; set; }
        public string DisplayName { get; set; }
    }
}
