using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class NewEventPage : ContentPage
    {
        private CancellationTokenSource _searchCts;

        private bool _mapaListo = false;
        private string _pendingJs = null;

        public NewEventPage()
        {
            InitializeComponent();
            
            MapaWebView.Navigated += OnMapaNavigated; 
        }

        private async void OnBuscarClicked(object sender, EventArgs e)
        {
            string query = NombreLugar.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query))
            {
                await DisplayAlert("", "Escribe un lugar para buscar.", "");
                return;
            }
            await BuscarUbicacion(query, CancellationToken.None);
        }
        private void OnMapaNavigated(object sender, WebNavigatedEventArgs e)
        {
            _mapaListo = true;
            
            if (_pendingJs != null)
            {
                MapaWebView.Eval(_pendingJs);
                _pendingJs = null;
            }
        }

        
        private async void OnLugarChanged(object sender, TextChangedEventArgs e)
        {
            string texto = e.NewTextValue?.Trim() ?? "";
            if (texto.Length < 3) return;

            
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(600, token); 
                await BuscarUbicacion(texto, token);
            }
            catch (TaskCanceledException) { }
        }

        private async Task BuscarUbicacion(string query, CancellationToken token)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HeatMeetApp/1.0");
                http.Timeout = TimeSpan.FromSeconds(10);

                
                string photonUrl = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(query)}&limit=5";
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                var photonResponse = await http.GetAsync(photonUrl, token);
                string photonJson = await photonResponse.Content.ReadAsStringAsync(token);
                var photonDoc = JsonDocument.Parse(photonJson);
                var features = photonDoc.RootElement.GetProperty("features");

                if (features.GetArrayLength() > 0)
                {
                    var opciones = new List<(string label, double lat, double lon)>();

                    foreach (var feature in features.EnumerateArray())
                    {
                        var coords = feature.GetProperty("geometry").GetProperty("coordinates");
                        double lon = coords[0].GetDouble();
                        double lat = coords[1].GetDouble();

                        var props = feature.GetProperty("properties");
                        string nombre = "";
                        if (props.TryGetProperty("name", out var n)) nombre = n.GetString() ?? "";
                        if (props.TryGetProperty("city", out var city)) nombre += $", {city.GetString()}";
                        if (string.IsNullOrWhiteSpace(nombre)) nombre = query;

                        opciones.Add((nombre, lat, lon));
                    }

                    if (opciones.Count == 1)
                    {
                        MoverMapa(opciones[0].lat, opciones[0].lon, opciones[0].label);
                        return;
                    }

                    string[] labels = opciones.Select(o => o.label).ToArray();
                    string elegido = await DisplayActionSheet("Elige la ubicación", "Cancelar", null, labels);
                    if (string.IsNullOrEmpty(elegido) || elegido == "Cancelar") return;
                    var sel = opciones.FirstOrDefault(o => o.label == elegido);
                    MoverMapa(sel.lat, sel.lon, sel.label);
                    return;
                }

                
                string nomUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=5&accept-language=es";
                string nomJson = await http.GetStringAsync(nomUrl, token);
                var resultados = JsonSerializer.Deserialize<List<NominatimResult>>(nomJson);

                if (resultados == null || resultados.Count == 0)
                {
                    await DisplayAlert("Sin resultados", "No se encontró. Prueba escribir la dirección exacta.", "OK");
                    return;
                }

                if (resultados.Count == 1)
                {
                    double lat = double.Parse(resultados[0].lat, System.Globalization.CultureInfo.InvariantCulture);
                    double lon = double.Parse(resultados[0].lon, System.Globalization.CultureInfo.InvariantCulture);
                    MoverMapa(lat, lon, resultados[0].display_name);
                }
                else
                {
                    var opciones2 = resultados
                        .Select(r => r.display_name.Length > 60 ? r.display_name.Substring(0, 60) + "..." : r.display_name)
                        .ToArray();
                    string elegido = await DisplayActionSheet("Elige la ubicación", "Cancelar", null, opciones2);
                    if (string.IsNullOrEmpty(elegido) || elegido == "Cancelar") return;
                    int index = Array.IndexOf(opciones2, elegido);
                    double lat = double.Parse(resultados[index].lat, System.Globalization.CultureInfo.InvariantCulture);
                    double lon = double.Parse(resultados[index].lon, System.Globalization.CultureInfo.InvariantCulture);
                    MoverMapa(lat, lon, resultados[index].display_name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Búsqueda error: {ex.Message}");
                await DisplayAlert("Error", $"Error: {ex.Message}", "OK");
            }
        }
        private void MoverMapa(double lat, double lon, string nombre)
        {
            string nombreSeguro = nombre.Replace("'", "\\'");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string js = $"moverMapa({lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{nombreSeguro}');";
                if (_mapaListo)
                    MapaWebView.Eval(js);
                else
                    _pendingJs = js;
            });
        }


        private async void CreateEvent_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NombreEvento.Text))
            {
                await DisplayAlert("Error", "El nombre del evento es obligatorio.", "");
                return;
            }

            int groupId = Preferences.Get("groupId", 0);
            if (groupId == 0)
            {
                await DisplayAlert("Error", "No hay grupo seleccionado.", "");
                return;
            }

            
            DateTime localDateTime = FechaPicker.Date + HoraPicker.Time;
            DateTime dateTimeUtc = localDateTime.ToUniversalTime();
            DateTime createDateUtc = DateTime.UtcNow;

            Socket? socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();

                NetworkMessage message = new()
                {
                    Command = "CREATE_EVENT",
                    Data = new
                    {
                        title = NombreEvento.Text.Trim(),
                        ubicacion = NombreLugar.Text?.Trim(),
                        direccionUrl = Direccion.Text?.Trim(),
                        fechaHora = dateTimeUtc,
                        createDate = createDateUtc,
                        groupId = groupId,
                        isEvent = true
                    }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    int newEventId = data.GetProperty("eventId").GetInt32();
                    Preferences.Set("eventId", newEventId);

                    await DisplayAlert("¡Listo!", "Evento creado correctamente.", "OK");

                    
                    NombreEvento.Text = string.Empty;
                    NombreLugar.Text = string.Empty;
                    Direccion.Text = string.Empty;
                    FechaPicker.Date = DateTime.Today;
                    HoraPicker.Time = TimeSpan.Zero;

                    await Navigation.PopAsync();
                }
                else
                {
                    string? serverMsg = response?.Data is JsonElement d2 &&
                                       d2.TryGetProperty("message", out JsonElement mp)
                                       ? mp.GetString() : "No se pudo crear el evento.";
                    await DisplayAlert("Error", serverMsg, "OK");
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
    }
}