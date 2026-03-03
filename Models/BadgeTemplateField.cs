using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BadgeCraft_Net.Models
{
    public class BadgeTemplateField
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BadgeTemplateId { get; set; }

        [ForeignKey("BadgeTemplateId")]
        [JsonIgnore]
        public BadgeTemplate BadgeTemplate { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty; 

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;  

        [Column(TypeName = "decimal(6,2)")]
        public decimal X { get; set; } 

        [Column(TypeName = "decimal(6,2)")]
        public decimal Y { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal Width { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal Height { get; set; }

        [StringLength(2000)]
        public string? StyleJson { get; set; }  

        public bool IsRequired { get; set; } = false;

        public string? DefaultValue { get; set; }
    }
}