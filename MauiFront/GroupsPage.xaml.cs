using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using Syncfusion.Maui.Scheduler;


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
        public ObservableCollection<SchedulerAppointment> EventosAgendados { get; set; }

        public GroupsPage()
        {
            InitializeComponent();

            // 1. Inicializar la lista de eventos
            EventosAgendados = new ObservableCollection<SchedulerAppointment>();

            // 2. Conectar los datos
            this.BindingContext = this;
            SchedulerControl.AppointmentsSource = EventosAgendados;

            // TODO: Personalizaremos los colores aquí después de que compile
        }

        private async void OnSchedulerTapped(object sender, SchedulerTappedEventArgs e)
        {
            // Si estamos en el MES, al tocar un día viajamos al DÍA
            if (SchedulerControl.View == SchedulerView.Month)
            {
                if (e.Element == SchedulerElement.SchedulerCell && e.Date.HasValue)
                {
                    SchedulerControl.DisplayDate = e.Date.Value;
                    SchedulerControl.View = SchedulerView.Day;
                }
            }
            // Si ya estamos en el DÍA, entonces sí pedimos el nombre del evento
            else if (SchedulerControl.View == SchedulerView.Day)
            {
                if (e.Element == SchedulerElement.SchedulerCell && e.Date.HasValue)
                {
                    string nombre = await DisplayPromptAsync("Nuevo Evento", "Nombre:", "OK", "Cancelar");
                    if (!string.IsNullOrWhiteSpace(nombre))
                    {
                        EventosAgendados.Add(new SchedulerAppointment
                        {
                            StartTime = e.Date.Value,
                            EndTime = e.Date.Value.AddHours(1),
                            Subject = nombre,
                            Background = Color.FromArgb("#FF6A00")
                        });
                    }
                }
            }
        }

        bool isFabOpen = false;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Task.Delay(100);//for possible bugs
            Shell.SetNavBarIsVisible(this, true);

            int userId = Preferences.Get("userId", 0);
            if (userId == 0) return;
            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "GET_USER_GROUPS",
                    Data = new { userId }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
                

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
            finally
            {
                if(socket != null) NetUtils.NetUtils.CloseSocket(socket);
            }
        }

        //Clic on group → go to chat
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

        
       
        
        private async void CreateNewGroup(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new CreateGroupPage());
        }

       
        private async void IrUnirseGrupo(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new JoinGroups()); 
        }

        
        private void VolverAlMes_Clicked(object sender, EventArgs e)
        {
            
            SchedulerControl.View = SchedulerView.Month;
        }
    }
}