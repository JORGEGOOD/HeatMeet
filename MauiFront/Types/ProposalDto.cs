using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiFront
{
    public class ProposalDto
    {
        public DateTime Fecha { get; set; }
        public int Count { get; set; }
        public bool EsCreador { get; set; } = false;
    }
}
