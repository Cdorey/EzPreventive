﻿using System.ComponentModel.DataAnnotations.Schema;

namespace EzNutrition.Shared.Data.Entities
{
    /// <summary>
    /// 个案人模型<br/>
    /// 包含7个维度的限定条件：年龄、性别、特殊生理时期、疾病状态、体型、活动强度<br/>
    /// 使用包括标准模型（树形派生，单一限定条件）和多条件模型（菱形派生）<br/>
    /// 框架无法直接判断模型派生的逻辑关系，需要建立时正确添加及维护
    /// </summary>
    //public class Person : RecordBase
    //{
    //    public Guid PersonId { get; set; }

    //    #region Conditions
    //    /// <summary>
    //    /// 年龄≥
    //    /// </summary>
    //    public decimal? AgeStart { get; set; }

    //    /// <summary>
    //    /// 年龄＜
    //    /// </summary>
    //    public decimal? AgeEnd { get; set; }

    //    /// <summary>
    //    /// 性别
    //    /// </summary>
    //    public string? Gender { get; set; }

    //    ///// <summary>
    //    ///// 特殊生理时期
    //    ///// </summary>
    //    //public string? SpecialPhysiologicalPeriod { get; set; }

    //    ///// <summary>
    //    ///// 疾病状态
    //    ///// </summary>
    //    //public string? Illness { get; set; }

    //    ///// <summary>
    //    ///// 体型
    //    ///// </summary>
    //    //public string? BodySize { get; set; }

    //    ///// <summary>
    //    ///// 活动强度
    //    ///// </summary>
    //    //public string? PhysicalActivityLevel { get; set; }

    //    #endregion

    //    public string? Cite { get; set; }

    //    /// <summary>
    //    /// 树形派生导航属性，子模型应为父模型的子集
    //    /// </summary>
    //    public Guid? DerivedFromPersonId { get; set; }
    //    public Person? DerivedFromPerson { get; set; }

    //    #region ProductionModel
    //    /// <summary>
    //    /// 菱形派生导航属性，标志多继承关系
    //    /// </summary>
    //    [InverseProperty(nameof(MultiDerivedPersonRelationship.ChildModel))]
    //    public List<MultiDerivedPersonRelationship>? MultiDerivedFrom { get; set; }

    //    [InverseProperty(nameof(MultiDerivedPersonRelationship.ParentModel))]
    //    public List<MultiDerivedPersonRelationship>? MultiDerivedTo { get; set; }

    //    public List<PersonalDietaryReferenceIntakeValue>? DietaryReferenceIntakes { get; set; }
    //    #endregion

    //    #region Comparators
    //    private IEnumerable<Person> GetDerivingTree()
    //    {
    //        var pointer = this;
    //        while (pointer.DerivedFromPerson != null)
    //        {
    //            yield return pointer.DerivedFromPerson;
    //            pointer = pointer.DerivedFromPerson;
    //        }
    //    }

    //    public bool IsDerivedFrom(Person person)
    //    {
    //        foreach (var item in GetDerivingTree())
    //        {
    //            if (item.PersonId == person.PersonId)
    //                return true;
    //        }
    //        return false;
    //    }

    //    public int Depth()
    //    {
    //        return GetDerivingTree().Count();
    //    }

    //    /// <summary>
    //    /// 合并两个模型的限定条件，其中+号左边的模型总是优先
    //    /// </summary>
    //    /// <param name="a"></param>
    //    /// <param name="b"></param>
    //    /// <returns></returns>
    //    public static Person operator +(Person a, Person b)
    //    {
    //        return new Person
    //        {
    //            PersonId = a.PersonId,
    //            AgeStart = a.AgeStart ?? b.AgeStart,
    //            AgeEnd = a.AgeEnd ?? b.AgeEnd,
    //            Gender = a.Gender ?? b.Gender,
    //            SpecialPhysiologicalPeriod = a.SpecialPhysiologicalPeriod ?? b.SpecialPhysiologicalPeriod,
    //            Illness = a.Illness ?? b.Illness,
    //            BodySize = a.BodySize ?? b.BodySize,
    //            PhysicalActivityLevel = a.PhysicalActivityLevel ?? b.PhysicalActivityLevel,
    //            Cite = $"Running time print model."
    //        };
    //    }

    //    public Person PrintMultiDerivedModel()
    //    {
    //        var parents = from item in MultiDerivedFrom
    //                      select item.ParentModel;
    //        var person = new Person();
    //        foreach (var item in parents)
    //        {
    //            person += item;
    //        }
    //        return person;
    //    }
    //    #endregion
    //}
}
