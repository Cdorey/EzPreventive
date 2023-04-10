using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Server.Data.Entities
{
    /// <summary>
    /// 个案人模型<br/>
    /// 包含7个维度的限定条件：年龄、性别、特殊生理时期、疾病状态、体型、活动强度<br/>
    /// 使用包括标准模型（树形派生，单一限定条件）和生产模型（菱形派生，关联DRIs数据）<br/>
    /// </summary>
    public class Person : RecordBase
    {
        public Guid PersonId { get; set; }

        #region Conditions
        /// <summary>
        /// 年龄≥
        /// </summary>
        public decimal? AgeStart { get; set; }

        /// <summary>
        /// 年龄＜
        /// </summary>
        public decimal? AgeEnd { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>
        /// 特殊生理时期
        /// </summary>
        public string? SpecialPhysiologicalPeriod { get; set; }

        /// <summary>
        /// 疾病状态
        /// </summary>
        public string? Illness { get; set; }

        /// <summary>
        /// 体型
        /// </summary>
        public string? BodySize { get; set; }

        /// <summary>
        /// 活动强度
        /// </summary>
        public string? PhysicalActivityLevel { get; set; }

        #endregion

        public string? Cite { get; set; }

        /// <summary>
        /// 树形派生导航属性，子模型应为父模型的子集
        /// </summary>
        public Guid? DerivedFromPersonId { get; set; }
        public Person? DerivedFromPerson { get; set; }

        #region ProductionModel
        /// <summary>
        /// 菱形派生导航属性，标志多继承关系
        /// </summary>
        [InverseProperty(nameof(MultiDerivedPersonRelationship.ChildModel))]
        public List<MultiDerivedPersonRelationship>? MultiDerivedFrom { get; set; }

        [InverseProperty(nameof(MultiDerivedPersonRelationship.ParentModel))]
        public List<MultiDerivedPersonRelationship>? MultiDerivedTo { get; set; }

        public List<PersonalDietaryReferenceIntakeValue>? DietaryReferenceIntakes { get; set; }
        #endregion
    }
}
