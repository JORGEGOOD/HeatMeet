using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiFront
{
    public class EventDto //<-- This will be an Event AND an aviability, bcs they share most of everything
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreateDate { get; set; }//TODO: Does the server send this? Check it

        public string? Location { get; set; }
        public string? AddressUrl { get; set; }
        public bool IsEvent { get; set; }// If its an event OR an Aviabilty
        public bool IsAllDay { get; set; } //To know if its and hour or the entire day
        public int? GroupId { get; set; }

        public string DateTimeFormatted => Date.ToLocalTime().ToString("dd/MM/yyyy  HH:mm");
    }
    
}
