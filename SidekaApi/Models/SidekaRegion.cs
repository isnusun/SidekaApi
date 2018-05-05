using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SidekaApi.Models {
    [Table("SdAllDesa")]
    public class SidekaRegion {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string RegionCode { get; set; }
        public string ParentCode { get; set; }
        public string RegionName { get; set; }
    }
}