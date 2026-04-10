using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HeatMeetServer
{
    public class OrmManager : DbContext
    {
        //create tables
        public DbSet<Users> Users { get; set; }
        public DbSet<Groups> Groups { get; set; }
        public DbSet<Events> Events { get; set; }
        public DbSet<Votes> Votes { get; set; }
        public DbSet<Messages> Messages { get; set; }

        //configuration
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)//config code
        { 
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseNpgsql(@$"Host=192.168.111.40;" +
                                    "Port=5432;" +
                                    "Username=Alumno;" + //school pc has this default user
                                    "Password=AlumnoIFP;" +
                                    "Database=HeatMeet;");
        }
    }

    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        //N:M
        public List<Groups> Groups { get; set; } = new();

        public List<UserAvailability> Availabilities { get; set; } = new();

    }

    public class UserAvailability
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime AvailableDate { get; set; } //Only the start date and is the entire day OR the individual hour.
                                                    //Then cliced on individual hours, 
                                                    //the entire hour is selected, and the users will select multiple hours 
        
        //public int? GroupId { get; set; } <-- Aviability is global (¿¿Aviability per group??)
    }




    public class Groups
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; }

        public string InviteCode { get; set; }
        public DateTime CreateDate { get; set; }


        public List<Users> Users { get; set; } = new();
        public List<Events> Events { get; set; } = new();//messages and events are separated
        public List<Messages> Messages { get; set; } = new(); //individual messages
    }

    public class Events
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Title { get; set; } = "Evento";
        public string? Location { get; set; }

        public string? AddressUrl { get; set; }
        public DateTime Date { get; set; }


        public int GroupId { get; set; }
        [ForeignKey("GroupId")]
        public Groups? Group { get; set; }

        public bool IsEvent { get; set; }
        public bool IsAllDay { get; set; }

        public List<Votes> Votes { get; set; } = new();
    }

    //public class EventDto //in Mauifront
    //{
    //    public int Id { get; set; } 
    //    public int UserId { get; set; } 
    //    public string Title { get; set; }
    //    public DateTime Date { get; set; } 

    //    public bool IsEvent { get; set; }
    //    public bool IsAllDay { get; set; } 
    //}


    public class Votes
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }
        public TimeSpan HourStart { get; set; }
        public TimeSpan HourEnd { get; set; }
        
        //foreign key to user
        public int UserId { get; set; }
        public Users User { get; set; }
        //foreign key to event
        public int EventId { get; set; }
        public Events Event { get; set; }
    }

    public class Messages
    {
        [Key]
        public int Id { get; set; }

        public string Content { get; set; }
        public DateTime CreateDate { get; set; }

        //foreign keys
        public int UserId { get; set; }
        public Users User { get; set; }
        public int GroupId { get; set; }
        public Groups Group { get; set; }
    }
}
