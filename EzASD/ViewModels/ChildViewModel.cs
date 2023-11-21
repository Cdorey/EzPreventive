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

        #region FamilyMembers

        private bool isFatherInFamily = true;

        public bool IsFatherInFamily { get => isFatherInFamily; set => SetProperty(ref isFatherInFamily, value); }

        private bool isMotherInFamily = true;

        public bool IsMotherInFamily { get => isMotherInFamily; set => SetProperty(ref isMotherInFamily, value); }

        private bool isGrandpaInFamily = false;

        public bool IsGrandpaInFamily { get => isGrandpaInFamily; set => SetProperty(ref isGrandpaInFamily, value); }

        private bool isGrandmaInFamily = false;

        public bool IsGrandmaInFamily { get => isGrandmaInFamily; set => SetProperty(ref isGrandmaInFamily, value); }

        private bool isNurseInFamily = false;

        public bool IsNurseInFamily { get => isNurseInFamily; set => SetProperty(ref isNurseInFamily, value); }

        private string othersInFamily = string.Empty;

        public string OthersInFamily { get => othersInFamily; set => SetProperty(ref othersInFamily, value); }

        private int fatherEduLevel;
        #endregion

        public int FatherEduLevel { get => fatherEduLevel; set => SetProperty(ref fatherEduLevel, value); }

        private int motherEduLevel;

        public int MotherEduLevel { get => motherEduLevel; set => SetProperty(ref motherEduLevel, value); }

        private int fatherProfession;

        public int FatherProfession { get => fatherProfession; set => SetProperty(ref fatherProfession, value); }

        private int motherProfession;

        public int MotherProfession { get => motherProfession; set => SetProperty(ref motherProfession, value); }

        private int parentsRelationship;

        public int ParentsRelationship { get => parentsRelationship; set => SetProperty(ref parentsRelationship, value); }

        private int fatherCharacter;

        public int FatherCharacter { get => fatherCharacter; set => SetProperty(ref fatherCharacter, value); }

        private int motherCharacter;

        public int MotherCharacter { get => motherCharacter; set => SetProperty(ref motherCharacter, value); }

        private int primaryEducator;

        public int PrimaryEducator { get => primaryEducator; set => SetProperty(ref primaryEducator, value); }

        private int mainEducationMethods;

        public int MainEducationMethods { get => mainEducationMethods; set => SetProperty(ref mainEducationMethods, value); }

        private int respondent;

        public int Respondent { get => respondent; set => SetProperty(ref respondent, value); }

        private int timeTogether;

        public int TimeTogether { get => timeTogether; set => SetProperty(ref timeTogether, value); }
    }
}
