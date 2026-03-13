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

    public class Users
    {
        [Key]
        public int Id { get; set; } // Propiedades siempre PUBLIC

        [Required, MaxLength(50)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        // Relación Muchos a Muchos con Groups
        public List<Groups> Groups { get; set; } = new();
    }

    public class Groups
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; }

        public string InviteCode { get; set; }
        public DateTime CreateDate { get; set; }

        // EF Core gestiona las listas automáticamente
        public List<Users> Users { get; set; } = new();
        public List<Events> Events { get; set; } = new();
        public List<Messages> Messages { get; set; } = new(); // Añadido para el chat de grupo
    }

    public class Events
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Title { get; set; }
        public string? Ubicacion { get; set; }

        // Relación con el grupo al que pertenece
        public int GroupId { get; set; }
        public Groups Group { get; set; }

        public List<Disponibility> Disponibilities { get; set; } = new();
    }

    public class Disponibility
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }
        public TimeSpan HourStart { get; set; }
        public TimeSpan HourEnd { get; set; }

        // FK hacia Usuario
        public int UserId { get; set; }
        public Users User { get; set; } // Propiedad de navegación

        // FK hacia Evento
        public int EventId { get; set; }
        public Events Event { get; set; }
    }

    public class Messages
    {
        [Key]
        public int Id { get; set; }

        public string Content { get; set; }
        public DateTime CreateDate { get; set; }
        public string? UrlFile { get; set; } // Mejor tenerlo por si acaso para fotos

        public int UserId { get; set; }
        public Users User { get; set; }

        public int GroupId { get; set; }
        public Groups Group { get; set; }
    }
}