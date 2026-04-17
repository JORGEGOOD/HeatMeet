using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class VotePage : ContentPage
    {
        private int _eventId;
        public Dictionary<DateTime, Color> DayColors { get; set; } = new();

        public VotePage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDraftEvent();
            await LoadGroupAvailability();
        }

        private async Task LoadDraftEvent()
        {
            int groupId = Preferences.Get("groupId", 0);
            if (groupId == 0) return;

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_LAST_EVENT",
                    Data = new { groupId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    _eventId = data.GetProperty("eventId").GetInt32();
                    TituloLabel.Text = data.GetProperty("title").GetString() ?? "";
                    UbicacionLabel.Text = data.TryGetProperty("ubicacion", out var u)
                                         ? (u.GetString() ?? "No especificada") : "No especificada";
                    DateTime fecha = data.GetProperty("fechaHora").GetDateTime().ToLocalTime();
                    FechaLabel.Text = fecha.ToString("dd/MM/yyyy");
                    HoraLabel.Text = fecha.ToString("HH:mm");
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

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_GROUP_AVAILABILITY",
                    Data = new { groupId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = JsonSerializer.Deserialize<List<AvailabilityDto>>(
                                   data.GetProperty("availabilities").GetRawText(), options);

                    
                    var countPerDay = new Dictionary<DateTime, int>();
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
                        {
                            1 => Color.FromArgb("#FFE0B2"),
                            2 => Color.FromArgb("#FFB347"),
                            3 => Color.FromArgb("#FF6A00"),
                            _ => Color.FromArgb("#E63900")
                        };
                    }

                    // Forzar refresco del scheduler
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SchedulerControl.MonthView = new Syncfusion.Maui.Scheduler.SchedulerMonthView
                        {
                            CellTemplate = BuildMonthCellTemplate()
                        };
                    });
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
                var grid = new Grid();

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
                            bg.Color = color;
                        else
                        {
                            bg.Color = Colors.White;
                        }
                            
                            
                    }
                };

                return grid;
            });
        }
        private async Task Votar(bool accepts)
        {
            int userId = Preferences.Get("userId", 0);
            AceptarBtn.IsEnabled = false;
            RechazarBtn.IsEnabled = false;

            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "VOTE_EVENT",
                    Data = new { eventId = _eventId, userId, accepts }
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
                    AceptarBtn.IsEnabled = true;
                    RechazarBtn.IsEnabled = true;
                }
            }

            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                AceptarBtn.IsEnabled = true;
                RechazarBtn.IsEnabled = true;
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
