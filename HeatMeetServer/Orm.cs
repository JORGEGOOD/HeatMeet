using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HeatMeetServer
{
    internal class OrmManager : DbContext
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
            optionsBuilder.UseNpgsql(@$"Host=LocalHost;" +
                                    "Port=5432;" +
                                    "Username=alumno;" + //school pc has this default user
                                    "Password=AlumnoFP;" +
                                    "Database=postgres;");
        }
    }

    [Table("Users")]
    public class Users
    {
        [Key]
        int id { get; set; }

        [Required, MaxLength(50)]
        string name { get; set; }

        [Required, MaxLength(100)]
        string email { get; set; }

        [Required, MaxLength(100)]
        string password { get; set; }

        List<Groups> Groups { get; set; } = new();

        /* related possible functions:
         * bool Login(string email, string passw) 
         * bool UpdateProfile(string nuevoNombre)
         * List<Messages> GetNotifications()
         */
    }

    [Table("Groups")]
    public class Groups
    {
        [Key]
        int id { get; set; }

        [Required, MaxLength(50)]
        string name { get; set; }

        [Required]
        string inviteCode { get; set; }

        [Required]
        DateTime createDate { get; set; }

        List<Users> usersId { get; set; } = new();//Should save users id's. 

        List<Events> eventsId { get; set; } = new();//Should save events id's.
        
        //the group should have a list of messages? the messages should be able to be events? or should the events be put separated?

        /* related possible functions:
         * string GenerateInviteLink()
         * bool AddMember(int userId)
         * bool BanUser(int userId)
         */
    }

    [Table("Events")]
    public class Events //an event is a message that opens a menu where multiple people can vote where and when to meet, voting place, and date
    {
        [Key]
        int id { get; set; }

        [Required, MaxLength(50)]
        string title { get; set; }

        string ubication { get; set; }

        List<Votes> votes { get; set; } = new();

        /* related possible functions:
         * bool CalculateHeatMap() //the app will show the votes as a heatmap on the calendar
         * bool SetPlace(string direction)
         * List<Messages> GetChat() 
         */
    }

    [Table("Votes")]
    public class Votes
    {
        [Key]
        int id { get; set; }

        [Required]
        DateTime date { get; set; }

        [Required]
        TimeSpan hourStart { get; set; }

        
        TimeSpan hourEnd { get; set; }

        [Required]
        int userid { get; set; }//link it to users? Users userid {get;set;}?

        /* related possible functions:
         * bool RegisterVote(DateTime date, 
         */
    }

    [Table("Messages")]
    public class Messages
    {
        [Key]
        int id { get; set; }

        [Required]
        int userId { get; set; }

        [Required]
        DateTime createDate { get; set; }

        [Required]
        string content { get; set; } //simple text no more, maybe in future we could put custom codes like <imgs> for the app to render it as an image                                  

        //string UrlFile? unnecesary? in the content we could put custom codes and the serialized img, i dont know

        /* related possible functions:
         * bool SendMessage
         * string UploadMedia(byte[] file)
         */
    }
}