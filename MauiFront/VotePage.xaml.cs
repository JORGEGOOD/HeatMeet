using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class VotePage : ContentPage
    {
        private int eventId;
        private int groupId;
        public Dictionary<DateTime, Color> DayColors { get; set; } = new();
        public class ProposalDto
        {
            public DateTime Fecha { get; set; }
            public int Count { get; set; }
        }

        public VotePage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            eventId = Preferences.Get("eventId",0);
            if (eventId == 0) { await DisplayAlert("Error", "eventId es 0", "Ok"); }

            await LoadDraftEvent();
            if (SchedulerControl.MonthView != null)
            {
                SchedulerControl.MonthView.CellTemplate = BuildMonthCellTemplate();
            }
            //Load Aviabilities
            _ = LoadGroupAvailability();

            //Request 3 best proposals to server 
            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new()
                {
                    Command = "GET_EVENT_PROPOSALS",
                    Data = new { eventId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);
                
                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var topDays = JsonSerializer.Deserialize<List<ProposalDto>>(data.GetProperty("topDays").GetRawText(), options);

                    MainThread.BeginInvokeOnMainThread(() => {
                        ProposalsContainer.Children.Clear();

                        if (topDays == null || topDays.Count == 0)
                        {
                            ProposalsContainer.Children.Add(new Label { Text = "No hay disponibilidades marcadas en el grupo todavía.", HorizontalOptions = LayoutOptions.Center });
                            return;
                        }

                        foreach (var prop in topDays)
                        {
                            //ProposalsContainer.Children.Add(CreateProposalCard(prop));
                        }
                    });
                }
                else
                {
                    await DisplayAlert("Error", "Error: Ha habido un error ", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex) {await DisplayAlert("Error", ex.Message, "OK");}
            finally {if (socket != null) NetUtils.NetUtils.CloseSocket(socket);}
        }

        //private View CreateProposalCard(ProposalDto prop)
        //{
        //    var card = new Frame
        //    {
        //        BorderColor = Color.FromArgb("#FF6A00"),
        //        CornerRadius = 15,
        //        Padding = 15,
        //        Content = new Grid
        //        {
        //            ColumnDefinitions = new ColumnDefinitionCollection {
        //        new ColumnDefinition { Width = GridLength.Star },
        //        new ColumnDefinition { Width = GridLength.Auto }
        //    },
        //            Children = {
        //        new VerticalStackLayout {
        //            Children = {
        //                new Label { Text = prop.Fecha.ToString("dddd, dd MMMM"), FontAttributes = FontAttributes.Bold },
        //                new Label { Text = $"{prop.Count} personas disponibles", FontSize = 12, TextColor = Colors.Gray }
        //            }
        //        },
        //        new Button {
        //            Text = "Votar",
        //            CommandParameter = prop.Fecha, // Guardamos la fecha elegida
        //            BackgroundColor = Color.FromArgb("#FF6A00"),
        //            TextColor = Colors.White,
        //            HeightRequest = 40,
        //            CornerRadius = 20
        //        }.Bind(Button.CommandProperty, nameof(VotarPropuestaCommand)) // Necesitarás un Command o evento Clicked
        //    }
        //        }
        //    };

        //    // Si prefieres usar Clicked en vez de Commands:
        //    var btn = (Button)((Grid)card.Content).Children[1];
        //    btn.Clicked += async (s, e) => await EnviarVotoFecha(prop.Fecha);

        //    return card;
        //}


        private async Task LoadDraftEvent()
        {
            groupId = Preferences.Get("groupId", 0);
            if (groupId == 0) return;

            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new()
                {
                    Command = "GET_EVENT",
                    Data = new { groupId, eventId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    eventId             = data.GetProperty("eventId").GetInt32();
                    TituloLabel.Text    = data.GetProperty("title").GetString() ?? "";
                    UbicacionLabel.Text = data.TryGetProperty("ubicacion", out var u) ? (u.GetString() ?? "No especificada") : "No especificada";
                    DateTime fecha      = data.GetProperty("fechaHora").GetDateTime().ToLocalTime();
                    FechaLabel.Text     = fecha.ToString("dd/MM/yyyy");
                    HoraLabel.Text      = fecha.ToString("HH:mm");
                }
                else
                {
                    await DisplayAlert("Sin propuesta", "No hay ninguna propuesta activa.", "OK");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
            }
        }
        
        private async void OnAceptar(object sender, EventArgs e)
            => await Votar(true);

        private async void OnRechazar(object sender, EventArgs e)
            => await Votar(false);
        private async Task LoadGroupAvailability()
        {
            int groupId = Preferences.Get("groupId", 0);
            if (groupId == 0) return;

            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_GROUP_AVAILABILITY",
                    Data = new { groupId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = JsonSerializer.Deserialize<List<AvailabilityDto>>(
                                   data.GetProperty("availabilities").GetRawText(), options);

                    Dictionary<DateTime, int> countPerDay = new();
                    if (list != null)
                    {
                        foreach (var av in list)
                        {
                            DateTime day = av.Date.ToLocalTime().Date;
                            if (!countPerDay.ContainsKey(day))
                                countPerDay[day] = 0;
                            countPerDay[day]++;
                        }
                    }

                    DayColors.Clear();
                    foreach (var kvp in countPerDay)
                    {
                        DayColors[kvp.Key] = kvp.Value switch
                        {//More people can a day --> more red it paints
                            1 => Color.FromArgb("#FFE0B2"),
                            2 => Color.FromArgb("#FFB347"),//TODO: Swap '1','2','3','4' to '20%','50%','80%'.
                            3 => Color.FromArgb("#FF6A00"),
                            _ => Color.FromArgb("#E63900")
                        };
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (SchedulerControl.MonthView == null)
                        {
                            SchedulerControl.MonthView = new Syncfusion.Maui.Scheduler.SchedulerMonthView();
                        }

                        SchedulerControl.MonthView.CellTemplate = null;
                        SchedulerControl.MonthView.CellTemplate = BuildMonthCellTemplate();
                    });
                }
                // Después de recibir la respuesta del socket
                if (response == null || response.Data == null)
                {
                    // Si falla la red, no hacemos nada o limpiamos, pero NO lanzamos error
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadGroupAvailability error: {ex.Message}");
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
            }
        }
        private DataTemplate BuildMonthCellTemplate()
        {
            return new DataTemplate(() =>
            {
                
                var grid = new Grid { BackgroundColor = Colors.Transparent };

                var bg = new BoxView
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Color = Colors.White 
                };

                var label = new Label
                {
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#222")
                };

                grid.Children.Add(bg);
                grid.Children.Add(label);

                grid.BindingContextChanged += (s, e) =>
                {
                    if (grid.BindingContext is Syncfusion.Maui.Scheduler.SchedulerMonthCellDetails details)
                    {
                        DateTime day = details.DateTime.Date;
                        label.Text = details.DateTime.Day.ToString();


                        if (DayColors.TryGetValue(day, out Color color))
                        {
                            bg.Color = color;
                        }
                        else
                        {
                            bg.Color = Colors.White;
                        }
                    }
                };

                return grid;
            });
        }
        private async Task Votar(bool accepts)//This is currently disabled
        {
            int userId = Preferences.Get("userId", 0);
            //AceptarBtn.IsEnabled = false;
            //RechazarBtn.IsEnabled = false;

            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "VOTE_EVENT",
                    Data = new { eventId = eventId, userId, accepts }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    string result = data.GetProperty("result").GetString() ?? "";

                    if (result == "confirmed")
                        await DisplayAlert(" ¡Confirmado!", "Todos han aceptado. ¡El evento está confirmado!", "OK");
                    else if (result == "deleted")
                        await DisplayAlert(" Cancelado", "El evento ha sido cancelado.", "OK");
                    else
                    {
                        int votes = data.TryGetProperty("votes", out var v) ? v.GetInt32() : 0;
                        int total = data.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
                        await DisplayAlert("Voto registrado", $"Han votado {votes} de {total} personas.", "OK");
                    }

                    await Navigation.PopAsync();
                }
                else
                {
                    string? msg = response?.Data is JsonElement d2 &&
                                 d2.TryGetProperty("message", out JsonElement mp)
                                 ? mp.GetString() : "Error al votar.";
                    await DisplayAlert("Error", msg, "OK");
                    //AceptarBtn.IsEnabled = true;
                    //RechazarBtn.IsEnabled = true;
                }
            }

            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                //AceptarBtn.IsEnabled = true;
                //RechazarBtn.IsEnabled = true;
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
            }

        }
        private void OnSchedulerTapped(object sender, Syncfusion.Maui.Scheduler.SchedulerTappedEventArgs e)
        {
            
        }
    }
}
