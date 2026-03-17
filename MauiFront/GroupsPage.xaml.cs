using System.Text.RegularExpressions;

namespace MauiFront
{
    public class Grupo
    {
        public string Nombre { get; set; }
        public string ImagenUrl { get; set; }
    }


    public partial class GroupsPage : ContentPage
    {
        public GroupsPage()
        {
            InitializeComponent();


            // Creamos datos inventados
            var misGrupos = new List<Grupo>
            {
                new Grupo { Nombre = "Equipo de Desarrollo", ImagenUrl = "dotnet_bot.png" },
                new Grupo { Nombre = "Fútbol Viernes", ImagenUrl = "dotnet_bot.png" },
                new Grupo { Nombre = "Amigos HeatMeet", ImagenUrl = "dotnet_bot.png" }
            };

            // Le pasamos los datos a la lista del XAML
            GroupsCollection.ItemsSource = misGrupos;

        }


        private async void CrearNuevoGrupo(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("newGroup");
        }



    }

}
