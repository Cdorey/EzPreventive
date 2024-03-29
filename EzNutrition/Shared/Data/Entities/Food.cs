﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EzNutrition.Shared.Data.Entities
{
    public class Food : RecordBase
    {
        public Guid FoodId { get; set; }

        [Required(ErrorMessage = $"{nameof(FriendlyCode)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? FriendlyCode { get; set; }

        public string? Cite { get; set; }

        public List<FoodNutrientValue>? FoodNutrientValues { get; set; }
    }

}
