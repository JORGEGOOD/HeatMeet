using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiFront
{
    public class GroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InviteCode { get; set; }
        public DateTime CreateDate { get; set; }
        public List<object> Users { get; set; } = new();
        public List<object> Events { get; set; } = new();
        public List<object> Messages { get; set; } = new();
        public string DisplayImage => "logo.png";
    }
}
