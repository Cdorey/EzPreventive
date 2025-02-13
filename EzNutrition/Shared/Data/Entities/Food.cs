using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EzNutrition.Shared.Data.Entities
{
    public class Food : RecordBase
    {
        public Guid FoodId { get; set; }

        /// <summary>
        /// 食物编码
        /// </summary>
        [Required(ErrorMessage = $"{nameof(FriendlyCode)}is required")]
        [StringLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? FriendlyCode { get; set; }

        public string? Cite { get; set; }

        /// <summary>
        /// 可食部
        /// </summary>
        public int? EdiblePortion { get; set; }

        /// <summary>
        /// 宝塔分类
        /// </summary>
        public string? FoodGroups { get; set; }

        [JsonIgnore]
        public List<FoodNutrientValue>? FoodNutrientValues { get; set; }
    }

}
