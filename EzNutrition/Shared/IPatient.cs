using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzNutrition.Shared
{
    public interface IPatient
    {
        public Guid PatientID { get; set; }

        public string Gender { get; set; }

        public double Height { get; set; }

        public decimal Weight { get; set; }

        public int Age { get; }
    }
}
