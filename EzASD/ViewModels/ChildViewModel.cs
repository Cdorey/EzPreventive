using AdonisUI.ViewModels;
using EzASD.Data.Entities;
using System;

namespace EzASD.ViewModels
{
    public class ChildViewModel : PropertyChangedBase
    {
        private readonly Child _innerModel;

        public ChildViewModel(Child innerModel)
        {
            _innerModel = innerModel;
        }

        public Guid Id => _innerModel.ChildId;

        public string? ChildName
        {
            get { return _innerModel.ChildName; }
            set
            {
                if (_innerModel.ChildName != value)
                {
                    _innerModel.ChildName = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Gender
        {
            get { return _innerModel.Gender; }
            set
            {
                if (_innerModel.Gender != value)
                {
                    _innerModel.Gender = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTime BirthDate
        {
            get { return _innerModel.BirthDate; }
            set
            {
                if (_innerModel.BirthDate != value)
                {
                    _innerModel.BirthDate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsOnlyChild
        {
            get { return _innerModel.IsOnlyChild; }
            set
            {
                if (_innerModel.IsOnlyChild != value)
                {
                    _innerModel.IsOnlyChild = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string? ContactNumber
        {
            get { return _innerModel.ContactNumber; }
            set
            {
                if (_innerModel.ContactNumber != value)
                {
                    _innerModel.ContactNumber = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #region FamilyMembers


        public bool IsFatherInFamily
        {
            get => _innerModel.IsFatherInFamily;
            set
            {
                if (_innerModel.IsFatherInFamily != value)
                {
                    _innerModel.IsFatherInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }


        public bool IsMotherInFamily
        {
            get => _innerModel.IsMotherInFamily;
            set
            {
                if (_innerModel.IsMotherInFamily != value)
                {
                    _innerModel.IsMotherInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }


        public bool IsGrandpaInFamily
        {
            get => _innerModel.IsGrandpaInFamily;
            set
            {
                if (_innerModel.IsGrandpaInFamily != value)
                {
                    _innerModel.IsGrandpaInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }


        public bool IsGrandmaInFamily
        {
            get => _innerModel.IsGrandmaInFamily;
            set
            {
                if (_innerModel.IsGrandmaInFamily != value)
                {
                    _innerModel.IsGrandmaInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }


        public bool IsNurseInFamily
        {
            get => _innerModel.IsNurseInFamily;
            set
            {
                if (_innerModel.IsNurseInFamily != value)
                {
                    _innerModel.IsNurseInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }


        public string OthersInFamily
        {
            get => _innerModel.OthersInFamily;
            set
            {
                if (_innerModel.OthersInFamily != value)
                {
                    _innerModel.OthersInFamily = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion

        public int FatherEduLevel
        {
            get => _innerModel.FatherEduLevel;
            set
            {
                if (_innerModel.FatherEduLevel != value)
                {
                    _innerModel.FatherEduLevel = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int MotherEduLevel
        {
            get => _innerModel.MotherEduLevel;
            set
            {
                if (_innerModel.MotherEduLevel != value)
                {
                    _innerModel.MotherEduLevel = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int FatherProfession
        {
            get => _innerModel.FatherProfession;
            set
            {
                if (_innerModel.FatherProfession != value)
                {
                    _innerModel.FatherProfession = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int MotherProfession
        {
            get => _innerModel.MotherProfession;
            set
            {
                if (_innerModel.MotherProfession != value)
                {
                    _innerModel.MotherProfession = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int ParentsRelationship
        {
            get => _innerModel.ParentsRelationship;
            set
            {
                if (_innerModel.ParentsRelationship != value)
                {
                    _innerModel.ParentsRelationship = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int FatherCharacter
        {
            get => _innerModel.FatherCharacter;
            set
            {
                if (_innerModel.FatherCharacter != value)
                {
                    _innerModel.FatherCharacter = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int MotherCharacter
        {
            get => _innerModel.MotherCharacter;
            set
            {
                if (_innerModel.MotherCharacter != value)
                {
                    _innerModel.MotherCharacter = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int PrimaryEducator
        {
            get => _innerModel.PrimaryEducator;
            set
            {
                if (_innerModel.PrimaryEducator != value)
                {
                    _innerModel.PrimaryEducator = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int MainEducationMethods
        {
            get => _innerModel.MainEducationMethods;
            set
            {
                if (_innerModel.MainEducationMethods != value)
                {
                    _innerModel.MainEducationMethods = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int Respondent
        {
            get => _innerModel.Respondent;
            set
            {
                if (_innerModel.Respondent != value)
                {
                    _innerModel.Respondent = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int TimeTogether
        {
            get => _innerModel.TimeTogether;
            set
            {
                if (_innerModel.TimeTogether != value)
                {
                    _innerModel.TimeTogether = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}
