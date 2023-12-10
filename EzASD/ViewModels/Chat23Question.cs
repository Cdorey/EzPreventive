using AdonisUI.ViewModels;

namespace EzASD.ViewModels
{
    public class Chat23Question : PropertyChangedBase
    {
        private int checkedIndex;

        public string Question { get; set; }

        public string[] Answers { get; set; }

        public int CheckedIndex
        {
            get => checkedIndex;
            set
            {
                checkedIndex = value;
                NotifyPropertyChanged();
            }
        }

        public int DefaultIndex { get; }

        public bool Positive
        {
            get
            {
                if (Answers.Length == 2)
                {
                    return DefaultIndex != CheckedIndex;
                }
                else if (DefaultIndex == 0)
                {
                    return CheckedIndex == 2 || CheckedIndex == 3;
                }
                else if (DefaultIndex == 3)
                {
                    return CheckedIndex == 0 || CheckedIndex == 1;
                }
                else
                {
                    return false;
                }
            }
        }

        public Chat23Question(string question, int defaultCheck, params string[] anwsers)
        {
            Question = question;
            Answers = anwsers;
            CheckedIndex = defaultCheck;
            DefaultIndex = defaultCheck;
        }
    }
}
