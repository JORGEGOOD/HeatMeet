using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiFront
{
    //ChatItem includes everything a chat can have. For the moment we have text messages and events, but is expandable
    public class ChatItem 
    {
        public DateTime CreateDate { get; set; }

        public MessageDto? Message { get; set; }
        public EventDto? Event { get; set; }


        public bool IsMessage => Message != null;
        public bool IsEvent => Event != null;

        //Who created it
        public int AuthorId => IsMessage ? Message.UserId : (Event?.UserId ?? 0);
    }
}
