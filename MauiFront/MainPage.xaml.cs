using SharedModels;
using NetUtils;
using System.Text.Json;
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
                var socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);
                var message = new SharedModels.NetworkMessage
                {
                    Command = "LOGIN",
                    Data = new { email, password }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                var response = NetUtils.NetUtils.ReceiveJson<SharedModels.NetworkMessage>(socket);
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
    }
}