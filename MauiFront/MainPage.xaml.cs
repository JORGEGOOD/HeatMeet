using SharedModels;
using NetUtils;
using System.Text.Json;
using System.Net.Sockets;
namespace MauiFront
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        public MainPage()
        {
            InitializeComponent();
        }
        private async void BotonEntrar(object sender, EventArgs e)
        {
            string email = userEntry.Text;
            string password = passwordEntry.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Error", "Pon usuario y contraseña!", "Ok");
                return;
            }

            try
            {
                Socket socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "LOGIN",
                    Data = new { email, password }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
                NetUtils.NetUtils.CloseSocket(socket);

                if (response.Data is not JsonElement data)
                {
                    await DisplayAlert("Error", "Respuesta inválida del servidor", "OK");
                    return;
                }

                bool ok = data.GetProperty("success").GetBoolean();
                string msg = data.GetProperty("message").GetString();

                if (ok)
                {

                    int userId = data.GetProperty("userId").GetInt32();
                    Preferences.Set("userId", userId);
                    await Shell.Current.GoToAsync("groups");
                }
                else
                {
                    await DisplayAlert("Error", msg, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "No se pudo conectar al servidor: " + ex.Message, "OK");
            }
        }

        private async void BotonRegistrar(object sender, EventArgs e)
        {
            string name = await DisplayPromptAsync("Registro", "Tu nombre:");
            if (string.IsNullOrWhiteSpace(name)) return;

            string email = await DisplayPromptAsync("Registro", "Tu email:");
            if (string.IsNullOrWhiteSpace(email)) return;

            string password = await DisplayPromptAsync("Registro", "Tu contraseña:", maxLength: 50);
            if (string.IsNullOrWhiteSpace(password)) return;

            try
            {
                Socket socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "REGISTER",
                    Data = new { name, email, password }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                SharedModels.NetworkMessage response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
                NetUtils.NetUtils.CloseSocket(socket);

                if (response.Data is not JsonElement data)
                {
                    await DisplayAlert("Error", "Respuesta inválida del servidor", "OK");
                    return;
                }

                bool ok = data.GetProperty("success").GetBoolean();
                string msg = data.GetProperty("message").GetString() ?? "";

                await DisplayAlert(ok ? "Éxito" : "Error", msg, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "No se pudo conectar al servidor: " + ex.Message, "OK");
            }
        }

    }
}