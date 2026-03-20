using SharedModels;
using NetUtils;
using System.Text.Json;

namespace MauiFront;

public partial class CreateGroupPage : ContentPage
{
    public CreateGroupPage()
    {
        InitializeComponent();
    }

    private async void OnCreateGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupNameEntry.Text))
        {
            await DisplayAlert("Error", "Por favor escribe el nombre del grupo", "OK");
            return;
        }

        int userId = Preferences.Get("userId", 0);
        if (userId == 0)
        {
            await DisplayAlert("Error", "No hay sesión activa", "OK");
            return;
        }

        try
        {
            var socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);
            var message = new SharedModels.NetworkMessage
            {
                Command = "CREATE_GROUP",
                Data = new { groupName = GroupNameEntry.Text, userId }
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
            string msg = data.GetProperty("message").GetString() ?? "";

            if (ok)
            {
                string inviteCode = data.GetProperty("inviteCode").GetString() ?? "";
                await DisplayAlert("Grupo creado", $"Código de invitación: {inviteCode}", "OK");
                await Navigation.PushAsync(new GroupsChat());
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

    private async void OnJoinGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupCodeEntry.Text))
        {
            await DisplayAlert("Error", "Por favor escribe el código del grupo", "OK");
            return;
        }

        int userId = Preferences.Get("userId", 0);
        if (userId == 0)
        {
            await DisplayAlert("Error", "No hay sesión activa", "OK");
            return;
        }

        try
        {
            var socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);
            var message = new SharedModels.NetworkMessage
            {
                Command = "JOIN_GROUP",
                Data = new { inviteCode = GroupCodeEntry.Text.Trim().ToUpper(), userId }
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
            string msg = data.GetProperty("message").GetString() ?? "";

            if (ok)
                await Navigation.PushAsync(new GroupsChat());
            else
                await DisplayAlert("Error", msg, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo conectar al servidor: " + ex.Message, "OK");
        }
    }
}