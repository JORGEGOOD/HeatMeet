using Syncfusion.Maui.Scheduler;
using System.Collections.ObjectModel;
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
        public int? GroupId { get; set; }
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

            if (e.Element == SchedulerElement.Appointment || !e.Date.HasValue)
            {
                // Aquí podrías poner lógica para BORRAR el evento si quieres,
                // pero por ahora pongamos un return para evitar el error.
                return;
            }


            //-- IF TOGGLE SWITCH IS ON -- 
            if (DisponibilidadSwitch.IsToggled)
            {
                if(e.Element != SchedulerElement.SchedulerCell && !e.Date.HasValue) return;

                // Un/Mark the day/hour as disponible

                //Search if it was marked or unmarked
                DateTime dateSelected = e.Date.Value.Date.ToUniversalTime();
                SchedulerAppointment? marked = ScheduledEvents.Cast<SchedulerAppointment>()                    
                           .FirstOrDefault(x => x.StartTime.Date == dateSelected && x.Id.ToString()=="-1");
                if (marked != null)
                {//If its marked, delete it
                    
                    ScheduledEvents.Remove(marked);

                    //Send server delete Aviability
                    System.Net.Sockets.Socket? socket = null;
                    try
                    {
                        socket = NetUtils.NetUtils.ConnectToServer();
                        SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                        {
                            Command = "SAVE_AVIABILITY",
                            Data = new
                            {
                                userId = Preferences.Get("userId", 0),
                                dateSelected = dateSelected,
                                isAllDay = SchedulerControl.View != SchedulerView.Day//if its month view, disp. is all day
                            }
                        };
                        NetUtils.NetUtils.SendJson(socket, message);
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
                else
                {//If its unmarked, create it
                    SchedulerAppointment newAviab = new SchedulerAppointment
                    {
                        Id = -1, //<--This will save us from a problem above this command
                        Subject = "🔴 Disponible",
                        StartTime = e.Date.Value.Date,
                        EndTime = e.Date.Value.Date.AddDays(1).AddSeconds(-1),
                        IsAllDay = true,
                        Background = Color.FromArgb("#FF0000") //Alternative smooth red: #E57373
                    };
                    ScheduledEvents.Add(newAviab);

                    //Send server the new aviability
                    var dto = new EventDto//SchedulerAppointment is private so it can't have "IsEvent" so this is a dupe
                    {
                        UserId = Preferences.Get("userId", 0),
                        Title = newAviab.Subject,
                        Date = newAviab.StartTime,
                        IsEvent = false,
                        IsAllDay = newAviab.IsAllDay,
                        //NO groupId, aviability is for everyone
                    };

                    //Send server create Aviability
                    System.Net.Sockets.Socket? socket = null;
                    try
                    {
                        socket = NetUtils.NetUtils.ConnectToServer();
                        SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                        {
                            Command = "SAVE_AVIABILITY",
                            Data = new 
                            {
                                userId = dto.UserId,
                                dateSelected = dto.Date,
                                isAllDay = dto.IsAllDay
                            }
                        };
                        NetUtils.NetUtils.SendJson(socket, message);
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
                return;
            }

            //-- IF DISPONIBILITY SWITCH IS OFF --
            //If Month view, go to day view
            if (SchedulerControl.View == SchedulerView.Month)
            {
                SchedulerControl.DisplayDate = e.Date.Value;
                SchedulerControl.View = SchedulerView.Day;
            }
            else if (SchedulerControl.View == SchedulerView.Day)
            {
                //in the future, we may expand the event on clicked
                //For the moment we only create events on the group chat   
            }
        }

        bool isFabOpen = false;
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetNavBarIsVisible(this, true);

            int userId = Preferences.Get("userId", 0);
            if (userId == 0) return;
            System.Net.Sockets.Socket socket = null;
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
                if (response?.Data is JsonElement data)
                {
                    //await MainThread.InvokeOnMainThreadAsync(async () =>
                    //{
                    //    await DisplayAlert("DEBUG DATA", data.ToString(), "OK");
                    //});
                    
                    bool ok = data.GetProperty("success").GetBoolean();
                    if (ok)
                    {
                        //Separate thread because this can start huge lag spikes
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            ScheduledEvents.Clear();

                            if (data.TryGetProperty("events", out JsonElement listJson))
                            {
                                // Usamos opciones para ignorar mayúsculas/minúsculas
                                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };


                                List<EventDto>? mixedList =
                                    JsonSerializer.Deserialize<List<EventDto>>(listJson.GetRawText(),options);

                                foreach (EventDto eventDto in mixedList)
                                {
                                    if (eventDto.IsEvent)
                                    {
                                        ScheduledEvents.Add(new SchedulerAppointment
                                        {
                                            Id = eventDto.Id,
                                            Subject = eventDto.Title,
                                            StartTime = eventDto.Date.ToLocalTime(),
                                            EndTime = eventDto.Date.ToLocalTime().AddHours(1),
                                            Background = Color.FromArgb("FF6A00")
                                        });
                                    }
                                    else
                                    {
                                        ScheduledEvents.Add(new SchedulerAppointment
                                        {
                                            Id = eventDto.Id,
                                            Subject = eventDto.Title,
                                            StartTime = eventDto.Date.ToLocalTime(),
                                            EndTime = eventDto.IsAllDay
                                                ? eventDto.Date.ToLocalTime().Date.AddDays(1).AddSeconds(-1)
                                                : eventDto.Date.ToLocalTime().AddHours(1),
                                            IsAllDay = eventDto.IsAllDay,
                                            Background = Color.FromArgb("#4CAF50"),
                                        });
                                    }
                                }
                            }
                            if (ScheduledEvents.Count > 0)
                            {
                                // A veces Syncfusion necesita un pequeño empujón si la colección se limpia y llena muy rápido
                                var temp = ScheduledEvents;
                                SchedulerControl.AppointmentsSource = null;
                                SchedulerControl.AppointmentsSource = temp;
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
