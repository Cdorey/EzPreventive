namespace EzASD.ViewModels
{
    public class Chat23Question
    {
        public string Question { get; set; }

        public string[] Answers { get; set; }

        public int CheckedIndex { get; set; }

        public Chat23Question(string question, params string[] anwsers)
        {
            Question = question;
            Answers = anwsers;
        }
    }
}
