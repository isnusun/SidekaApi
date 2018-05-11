using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SidekaApi.Models
{
    [Table("SdDesa")]
    public class SidekaDesa
    {
        [Key]
        [JsonProperty("blog_id")]
        public int BlogId { get; set; }
        public string Domain { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Kode { get; set; }
        public string Desa { get; set; }
        public string Kecamatan { get; set; }
        public string Kabupaten { get; set; }
        public string Propinsi { get; set; }
        public string Kades { get; set; }
        public string Sekdes { get; set; }
        public string Pendamping { get; set; }
        [JsonProperty("is_dbt")]
        public bool IsDbt { get; set; }
        [JsonProperty("is_lokpri")]
        public bool IsLokpri { get; set; }
    }
}
