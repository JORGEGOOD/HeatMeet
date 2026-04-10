using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class VotePage : ContentPage
    {
        public VotePage()
        {
            InitializeComponent();
        }

        private async void CrearEvento_Clicked(object sender, EventArgs e)
        {
            
            if (string.IsNullOrWhiteSpace(NombreEvento.Text))
            {
                await DisplayAlert("Error", "El nombre del evento es obligatorio.", "OK");
                return;
            }

            int groupId = Preferences.Get("groupId", 0);
            if (groupId == 0)
            {
                await DisplayAlert("Error", "No hay grupo seleccionado.", "OK");
                return;
            }

            // Combinar fecha y hora
            DateTime fechaHora = FechaPicker.Date + HoraPicker.Time;

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();

                NetworkMessage message = new NetworkMessage
                {
                    Command = "CREATE_EVENT",
                    Data = new
                    {
                        title = NombreEvento.Text.Trim(),
                        ubicacion = NombreLugar.Text?.Trim(),
                        direccionUrl = Direccion.Text?.Trim(),
                        fechaHora = fechaHora,
                        groupId = groupId
                    }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    await DisplayAlert("¡Listo!", "Evento creado correctamente.", "OK");

                    // Limpiar formulario
                    NombreEvento.Text = string.Empty;
                    NombreLugar.Text = string.Empty;
                    Direccion.Text = string.Empty;
                    FechaPicker.Date = DateTime.Today;
                    HoraPicker.Time = TimeSpan.Zero;
                }
                else
                {
                    string serverMsg = response?.Data is JsonElement d2 &&
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