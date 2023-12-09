using System;

namespace EzASD.Data.Entities
{
    public class Chat23aRecord
    {
        public Guid Id { get; set; }

        public Guid ChildId { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public string Answer { get; set; } = string.Empty;

        public Child? Child { get; set; }

        public Chat23aRecord()
        {

        }
    }
}
