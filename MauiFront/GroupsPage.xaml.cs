using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using SharedModels;
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

    public class EventDto //<-- This will be an Event AND an aviability, bcs they share most of everything
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string? Location { get; set; }
        public string? AddressUrl { get; set; }
        public bool IsEvent { get; set; }// If its an event OR an Aviabilty
        public bool IsAllDay { get; set; } //To know if its and hour or the entire day
    }


    public partial class GroupsPage : ContentPage
    {
        //List of all the events the user has
        public ObservableCollection<SchedulerAppointment> ScheduledEvents { get; set; }

        //Disponibility button Toggle switch
        public bool IsVotingDisponibility { get; set; } = false;


        public GroupsPage()
        {
            InitializeComponent();

            // 1. Inicializar la lista de eventos
            ScheduledEvents = new ObservableCollection<SchedulerAppointment>();

            // 2. Conectar los datos
            this.BindingContext = this;
            SchedulerControl.AppointmentsSource = ScheduledEvents;

            // TODO: Personalizaremos los colores aquí después de que compile
        }

        private async void OnSchedulerTapped(object sender, SchedulerTappedEventArgs e)
        {
            //-- IF TOGGLE SWITCH IS ON --
            if(DisponibilidadSwitch.IsToggled)
            {
                //Get the day clicked to switch it to Can or Can't
                if(e.Element == SchedulerElement.SchedulerCell && e.Date.HasValue)
                {

                    //get in that day was On or Off. (In the appear screen a server database select should be done)
                        //check in the frontend if its painted or not? Or make a dedicated list as a copy of the server select and ignore frontend?

                    //depending on the select above, switch between Can or Can't that day (red or white color)

                    //Send the decision to the server


                }

                DisplayAlert("Mensaje", "Has dicho disponibilidad en este dia", "Ok");
                return;
            }


            //-- IF TOGGLE SWITCH IS OFF --

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
                        ScheduledEvents.Add(new SchedulerAppointment
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
            //--GET GROUPS FROM SERVER--
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "GET_USER_GROUPS",
                    Data = new { userId }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
                

                if (response.Data is JsonElement data)
                {
                    bool ok = data.GetProperty("success").GetBoolean();

                    if (ok)
                    {
                        JsonElement groupsJson = data.GetProperty("groups");
                        List<Group>? grupos = JsonSerializer
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

            //--GET USER EVENTS AND AVIABILITY--
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                //new command
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "GET_USER_EVENTS_AND_AVIABILITY",
                    Data = new { userId }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);

                //response
                if (response.Data is JsonElement data)
                {
                    bool ok = data.GetProperty("success").GetBoolean();

                    if (ok)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ScheduledEvents.Clear();
                            //process list
                            if(data.TryGetProperty("eventsAndAviability",out JsonElement listJson))
                            {
                                List<EventDto>? mixedList = JsonSerializer.Deserialize<List<EventDto>>(listJson.GetRawText());

                                foreach(EventDto eventDto in mixedList)
                                {
                                    if(eventDto.IsEvent)//Events
                                    {
                                        ScheduledEvents.Add(new SchedulerAppointment//Add event
                                        {
                                            Id = eventDto.Id,
                                            Subject = eventDto.Title,
                                            StartTime = eventDto.Date.Date,
                                            EndTime = eventDto.Date.Date.AddHours(1),//<-- By design we make each event have 1 hour duration
                                            Background = Color.FromArgb("FF6A00")//Event color
                                        });
                                    }
                                    else//Aviabilities //The entire day or just an hour?
                                    {
                                        ScheduledEvents.Add(new SchedulerAppointment
                                        {
                                            Id = eventDto.Id,
                                            Subject = eventDto.Title,
                                            StartTime = eventDto.Date,
                                            //New IsEntire day, to know if its the entire day or just a specific hour
                                            EndTime = (eventDto.IsAllDay ? eventDto.Date.Date.AddDays(1).AddSeconds(-1) : eventDto.Date.Date.AddHours(1)),
                                            IsAllDay = eventDto.IsAllDay,
                                            Background = Color.FromArgb("#4CAF50"),
                                        });
                                    }
                                }

                            }

                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "No se pudieron cargar los eventos: " + ex.Message, "OK");
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
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
