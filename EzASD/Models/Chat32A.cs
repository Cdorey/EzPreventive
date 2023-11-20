namespace EzASD.Models
{
    internal class Chat32A
    {
        public string Question { get; }

        public string[] Options { get; }

        public Chat32A(string question, string[]? options = default)
        {
            Question = question;
            Options = options ?? new string[]
            {
                "没有",
                "偶尔",
                "有时",
                "经常",
            };
        }
    }
}
