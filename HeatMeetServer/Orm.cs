using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HeatMeetServer
{
    internal class OrmManager : DbContext
    {
        //crear tablas


        //configuracion
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)//config code
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseNpgsql(@$"Host=LocalHost;" +
                                    "Port=5432;" +
                                    "Username=alumno;" + //school pc has this default user
                                    "Password=AlumnoIFP;" +
                                    "Database=postgres;");
        }
    }


    [Table("Users")]
    public class Users
    {
        [Key]
        int id { get; set; }

        [Required,MaxLength(50)]
        string name { get; set; }

        [Required,MaxLength(100)]
        string email { get; set; }

        [Required,MaxLength(100)]
        string password { get; set; }

        List<Groups> Groups { get; set; } = new();

        /*
         * bool Login(string email, string passw) 
         * void UpdateProfile(string nuevoNombre)
         * List<Messages> GetNotifications()
         */
    }

    [Table("Groups")]
    public class Groups
    {
        [Key]
        int id { get; set; }

        [Required,MaxLength(50)]
        string name { get; set; }

        [Required]
        string inviteCode { get; set; }

        [Required]
        DateTime createDate { get; set; }


        List<Users>  usersId  { get; set; } = new();  //deberia guardar las id's de los usuarios
        List<Events> eventsId { get; set; } = new();//deberia guardar cada id de evento
        //el grupo deberia tener una lista de mensajes? y esos mensajes contienen eventos? deberia ir eventos dentro de mensajes?
        
        /*
         * string GenerarEnlac
         */
    }


    [Table("Events")]
    public class Events
    {
        [Key]
        int id { get; set; }

        [Required,MaxLength(50)]
        string title { get; set; }

        string ubicacion { get; set; }//separar longitud y latitud?

        List<Disponibility> disponibility { get; set; } = new();

    }

    [Table("Disponibility")]
    public class Disponibility
    {
        [Key]
        int id { get; set; }

        [Required]
        DateTime date { get; set; }

        [Required]
        TimeSpan hourStart { get; set; }

        [Required]
        TimeSpan hourEnd {  get; set; }

        [Required]
        int userid { get; set; }//linkearlo a usuarios? Users userid {get;set;}?

    }

    [Table("Messages")]
    public class Messages
    {
        [Key]
        int id { get; set; }

        [Required]
        string content { get; set; } //texto simple sin mas, quizas en un futuro ponemos un code personalizado como <img> y q la app lo detecte como imagen serializada

        //string UrlArchivo???? no hace falta, en el content podemos poner codigos personalizados? no lo tengo claro
        
        [Required]
        DateTime createDate { get; set; }

        
    }



}
