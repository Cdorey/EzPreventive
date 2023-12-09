using AdonisUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzASD.Models
{
    public class EarlyWarningSign : PropertyChangedBase
    {
        private bool isPositive = false;

        public string Age { get; }

        public bool IsPositive
        {
            get => isPositive;
            set
            {
                isPositive = value;
                NotifyPropertyChanged();
            }
        }

        public string Question { get; }

        public EarlyWarningSign(string age, string question)
        {
            Age = age;
            Question = question;
        }
    }
}
