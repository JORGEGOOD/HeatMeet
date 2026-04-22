using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class VotePage : ContentPage
    {
        private int eventId;
        private int groupId;
        private int userId;
        public Dictionary<DateTime, Color> DayColors { get; set; } = new();

        public VotePage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            eventId = Preferences.Get("eventId",0);
            groupId = Preferences.Get("groupId", 0);
            userId  = Preferences.Get("userId", 0);

            Console.WriteLine($"[VOTE_PAGE] EventID: {eventId}");

            await LoadDraftEvent();
            if (SchedulerControl.MonthView != null)
            {
                SchedulerControl.MonthView.CellTemplate = BuildMonthCellTemplate();
            }
            //Load Aviabilities
            _ = LoadGroupAvailability();

            //Proposals
            //Request 3 best proposals to server 
            Socket? socket = null;
            try
            {
                //Send command
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new()
                {
                    Command = "GET_EVENT_PROPOSALS",
                    Data = new { eventId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                //Response
                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    //Get proposals
                    JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
                    List<ProposalDto>? topDays = JsonSerializer.Deserialize<List<ProposalDto>>(data.GetProperty("topDays").GetRawText(), options);

                    Console.WriteLine($"[VOTE_PAGE] Lista deserializada. Items: {topDays.Count}");

                    Console.WriteLine($"[VOTE_PAGE] JSON Recibido: {data.GetProperty("topDays").GetRawText()}");
                    
                    //Add all the proposals
                    MainThread.BeginInvokeOnMainThread(() => {ProposalsCollection.ItemsSource = topDays;});
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

        //When a proposal is voted
        private async void OnVoteProposalClicked(object sender, EventArgs e)
        {
            Console.WriteLine("---- CLICK VOTE START ----");

            if (sender is not Button button)
            {
                Console.WriteLine("Sender no es Button");
                return;
            }

            if (button.CommandParameter is not ProposalDto selectedDate)
            {
                Console.WriteLine("CommandParameter no es ProposalDto");
                return;
            }

            Console.WriteLine($"SelectedDate: {selectedDate.Fecha}");

            bool confirm = await DisplayAlert("Confirmar", $"¿Votar por el {selectedDate.Fecha:dd/MM}?", "Sí", "No");

            Console.WriteLine($"Confirm result: {confirm}");

            if (!confirm)
            {
                Console.WriteLine("Usuario canceló");
                return;
            }

            Socket? socket = null;

            try
            {
                Console.WriteLine("Conectando al servidor...");
                socket = NetUtils.NetUtils.ConnectToServer();
                Console.WriteLine("Conectado");

                var payload = new
                {
                    userId,
                    eventId,
                    selectedDate = selectedDate.Fecha
                };

                Console.WriteLine($"Payload:");
                Console.WriteLine($"userId: {userId}");
                Console.WriteLine($"eventId: {eventId}");
                Console.WriteLine($"selectedDate: {selectedDate.Fecha}");

                NetworkMessage message = new()
                {
                    Command = "VOTE_EVENT",
                    Data = payload
                };

                Console.WriteLine("Enviando mensaje...");
                NetUtils.NetUtils.SendJson(socket, message);
                Console.WriteLine("Mensaje enviado");

                Console.WriteLine("Esperando respuesta...");
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response == null)
                {
                    Console.WriteLine("Response es NULL");
                    await DisplayAlert("Error", "Respuesta nula del servidor", "OK");
                    return;
                }

                Console.WriteLine($"Response recibida. Command: {response.Command}");

                if (response.Data == null)
                {
                    Console.WriteLine("Response.Data es NULL");
                    await DisplayAlert("Error", "Data nula en la respuesta", "OK");
                    return;
                }

                Console.WriteLine($"Tipo de Data: {response.Data.GetType()}");

                if (response.Data is not JsonElement data)
                {
                    Console.WriteLine("Data no es JsonElement");
                    await DisplayAlert("Error", "Formato de respuesta inválido", "OK");
                    return;
                }

                Console.WriteLine("JSON recibido:");
                Console.WriteLine(data.ToString());

                if (!data.TryGetProperty("success", out JsonElement successProp))
                {
                    Console.WriteLine("No existe 'success'");
                    await DisplayAlert("Error", "Respuesta sin campo success", "OK");
                    return;
                }

                bool success = successProp.GetBoolean();
                Console.WriteLine($"Success: {success}");

                if (!success)
                {
                    string errorMsg = data.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString() ?? "Error desconocido"
                        : "Error sin mensaje";

                    Console.WriteLine($"Error del server: {errorMsg}");
                    await DisplayAlert("Error", errorMsg, "OK");
                    return;
                }

                // 🔹 Extraer datos con seguridad
                string result = data.TryGetProperty("result", out var resultProp)
                    ? resultProp.GetString() ?? ""
                    : "";

                int current = data.TryGetProperty("current", out var currentProp)
                    ? currentProp.GetInt32()
                    : -1;

                int total = data.TryGetProperty("total", out var totalProp)
                    ? totalProp.GetInt32()
                    : -1;

                Console.WriteLine($"Result: {result}");
                Console.WriteLine($"Current: {current}");
                Console.WriteLine($"Total: {total}");

                if (result == "confirmed")
                {
                    await DisplayAlert("Evento creado", "Todos han votado, el evento se ha creado con éxito.", "Ok");
                }
                else if (result == "voted")
                {
                    await DisplayAlert("Voto Registrado", $"Has votado correctamente. Van {current} de {total} votos.", "Ok");
                }
                else
                {
                    Console.WriteLine("Result desconocido");
                    await DisplayAlert("Aviso", "Respuesta inesperada del servidor", "OK");
                }

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPCIÓN:");
                Console.WriteLine(ex.ToString());

                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                Console.WriteLine("Cerrando socket...");
                if (socket != null)
                {
                    NetUtils.NetUtils.CloseSocket(socket);
                    Console.WriteLine("Socket cerrado");
                }

                Console.WriteLine("---- CLICK VOTE END ----");
            }
        }

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
