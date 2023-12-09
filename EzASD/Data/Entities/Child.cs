using EzASD.Models;
using EzASD.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzASD.Data.Entities
{

    public class Child
    {
        public Guid ChildId { get; set; }

        public string? ChildName { get; set; }

        public string Gender { get; set; } = "男";

        public DateTime BirthDate { get; set; } = DateTime.Now;

        public bool IsOnlyChild { get; set; }

        public string? ContactNumber { get; set; }

        #region FamilyMembers

        public bool IsFatherInFamily { get; set; }

        public bool IsMotherInFamily { get; set; }


        public bool IsGrandpaInFamily { get; set; }

        public bool IsGrandmaInFamily { get; set; }

        public bool IsNurseInFamily { get; set; }

        public string OthersInFamily { get; set; } = string.Empty;

        #endregion

        public int FatherEduLevel { get; set; }

        public int MotherEduLevel { get; set; }

        public int FatherProfession { get; set; }


        public int MotherProfession { get; set; }


        public int ParentsRelationship { get; set; }


        public int FatherCharacter { get; set; }


        public int MotherCharacter { get; set; }


        public int PrimaryEducator { get; set; }


        public int MainEducationMethods { get; set; }


        public int Respondent { get; set; }


        public int TimeTogether { get; set; }

        public List<PositiveSignRecord>? PositiveSignRecords { get; set; }
    }
}
