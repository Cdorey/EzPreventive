using EzNutrition.Shared.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzNutrition.Shared.Utilities
{
    public static class EnergyCalculator
    {
        public static int GetEnergy(decimal height, decimal bee, decimal pal)
        {
            var bw = height - 105;
            return (int)Math.Round(bee * bw * pal);
        }
    }
}
