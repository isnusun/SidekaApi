using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    public class WordpressOption
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int64 OptionId { get; set; }

        public string OptionName { get; set; }
        public string OptionValue { get; set; }
        public string Autoload { get; set; }
    }
}
