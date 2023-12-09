using System;

namespace EzASD.Data.Entities
{
    public class PositiveSignRecord
    {
        public Guid Id { get; set; }

        public Guid ChildId { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public string PositiveQuestion { get; set; } = string.Empty;

        public Child? Child { get; set; }

        public PositiveSignRecord()
        {

        }
    }
}
