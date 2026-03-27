using System;
using System.Collections.Generic;

using System.Net.Sockets;
using System.Text.Json;

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
        public string DisplayImage => "logo.png";
    }

    public partial class GroupsPage : ContentPage
    {
        bool isFabOpen = false;
        public GroupsPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetNavBarIsVisible(this, true);

            int userId = Preferences.Get("userId", 0);
            if (userId == 0) return;

            try
            {
                Socket socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "GET_USER_GROUPS",
                    Data = new { userId }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
                NetUtils.NetUtils.CloseSocket(socket);

                if (response.Data is JsonElement data)
                {
                    bool ok = data.GetProperty("success").GetBoolean();

                    if (ok)
                    {
                        JsonElement groupsJson = data.GetProperty("groups");
                        var grupos = JsonSerializer
                            .Deserialize<List<Group>>(groupsJson.GetRawText());
                        GroupsCollection.ItemsSource = grupos;
                    }
                }

            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "No se pudieron cargar los grupos: " + ex.Message, "OK");
            }
        }

        // Pulsar un grupo → ir al chat
        private async void OnGroupTapped(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is Group grupo)
            {
                Preferences.Set("groupId", grupo.Id);
                Preferences.Set("groupName", grupo.Name);

                await Navigation.PushAsync(new GroupsChat());
            }
        }

        //Animation button create or join croup
        private async void ToggleFabMenu(object sender, EventArgs e)
        {
            if (!isFabOpen)
            {
                Overlay.IsVisible = true;

                CrearLayout.IsVisible = true;
                UnirseLayout.IsVisible = true;

                await Task.WhenAll(
                    Overlay.FadeTo(1, 200),

                    CrearLayout.FadeTo(1, 200),
                    CrearLayout.TranslateTo(0, -20, 200, Easing.SinOut),
                    CrearLayout.ScaleTo(1, 200),

                    UnirseLayout.FadeTo(1, 200),
                    UnirseLayout.TranslateTo(0, -20, 200, Easing.SinOut),
                    UnirseLayout.ScaleTo(1, 200),

                    FabButton.RotateTo(45, 200)
                );
            }
            else
            {
                await Task.WhenAll(
                    Overlay.FadeTo(0, 150),

                    CrearLayout.FadeTo(0, 150),
                    CrearLayout.ScaleTo(0.8, 150),

                    UnirseLayout.FadeTo(0, 150),
                    UnirseLayout.ScaleTo(0.8, 150),

                    FabButton.RotateTo(0, 200)
                );

                Overlay.IsVisible = false;
                CrearLayout.IsVisible = false;
                UnirseLayout.IsVisible = false;
            }

            isFabOpen = !isFabOpen;
        }

        
        private async void IrCrearGrupo(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new CreateGroupPage());
        }

       
        private async void IrUnirseGrupo(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new JoinGroups()); 
        }
    }
}