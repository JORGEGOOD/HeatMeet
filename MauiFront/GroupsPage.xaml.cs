using System;
using System.Collections.Generic;

namespace MauiFront
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InviteCode { get; set; }
        public DateTime CreateDate { get; set; }
        public List<object> Users { get; set; } = new();
        public List<object> Events { get; set; } = new();
        public List<object> Messages { get; set; } = new();
        public string DisplayImage => "dotnet_bot.png";
    }

    public partial class GroupsPage : ContentPage
    {
        public GroupsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var gruposDesdeServer = new List<Group>
            {
                new Group
                {
                    Id = 1,
                    Name = "Desarrolladores .NET",
                    InviteCode = "HTM-123",
                    CreateDate = DateTime.Now
                },
                new Group
                {
                    Id = 2,
                    Name = "Equipo HeatMeet",
                    InviteCode = "ABC-999",
                    CreateDate = DateTime.Now.AddDays(-10)
                }
            };

            GroupsCollection.ItemsSource = gruposDesdeServer;
        }

        // Pulsar un grupo → ir al chat
        private async void OnGroupTapped(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is Group grupo)
            {
                await Navigation.PushAsync(new GroupsChat());
            }
        }

        // Botón "+" → ir a crear/unirse a grupo
        private async void CrearNuevoGrupo(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new CreateGroupPage());
        }
    }
}