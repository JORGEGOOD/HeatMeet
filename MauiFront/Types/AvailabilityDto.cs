using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiFront
{
    public class AvailabilityDto
    {
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public bool IsAllDay { get; set; }
        public string Title { get; set; }
    }
}
