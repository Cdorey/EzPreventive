using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzASD.Models
{
    internal class EarlyWarningSign
    {
        public string Age { get; }

        public string Question { get; }

        public EarlyWarningSign(string age, string question)
        {
            Age = age;
            Question = question;
        }
    }
}
