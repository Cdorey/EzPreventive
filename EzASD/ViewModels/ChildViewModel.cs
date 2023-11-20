using AdonisUI.ViewModels;
using System;

namespace EzASD.ViewModels
{
    internal class ChildViewModel : PropertyChangedBase
    {
        private string? _childName;
        private string _gender = "男";
        private DateTime _birthDate = DateTime.Today;
        private bool _isOnlyChild = true;
        private string? _contactNumber;

        public string? ChildName
        {
            get { return _childName; }
            set
            {
                if (_childName != value)
                {
                    _childName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Gender
        {
            get { return _gender; }
            set
            {
                if (_gender != value)
                {
                    _gender = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTime BirthDate
        {
            get { return _birthDate; }
            set
            {
                if (_birthDate != value)
                {
                    _birthDate = value;
                    var year = DateTime.Today.Year - BirthDate.Year;
                    var month = DateTime.Today.Month - BirthDate.Month;
                    var day = DateTime.Today.Day - BirthDate.Day;
                    var calculator = year * 12 + month;
                    if (day < 0)
                    {
                        calculator -= 1;
                    }
                    EarlyWarningQes.CurrentAge = calculator switch
                    {
                        (>= 72) => "6y",
                        (>= 60) => "5y",
                        (>= 48) => "4y",
                        (>= 36) => "36m",
                        (>= 30) => "30m",
                        (>= 24) => "24m",
                        (>= 18) => "18m",
                        (>= 12) => "12m",
                        (>= 8) => "8m",
                        (>= 6) => "6m",
                        (>= 3) => "3m",
                        _ => EarlyWarningQes.CurrentAge
                    };
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsOnlyChild
        {
            get { return _isOnlyChild; }
            set
            {
                if (_isOnlyChild != value)
                {
                    _isOnlyChild = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string? ContactNumber
        {
            get { return _contactNumber; }
            set
            {
                if (_contactNumber != value)
                {
                    _contactNumber = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public EarlyWarningViewModel EarlyWarningQes { get; } = new();
    }
}
