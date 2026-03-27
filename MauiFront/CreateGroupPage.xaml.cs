using SharedModels;
using NetUtils;
using System.Text.Json;
using System.Net.Sockets;

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
            Socket socket = NetUtils.NetUtils.ConnectToServer();

            SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
            {
                Command = "CREATE_GROUP",
                Data = new { groupName = GroupNameEntry.Text, userId }
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

            if (ok)
            {
                int groupId = data.GetProperty("groupId").GetInt32();
                string groupName = data.GetProperty("groupName").GetString() ?? "";

                Preferences.Set("groupId", groupId);
                Preferences.Set("groupName", groupName);

                string inviteCode = data.GetProperty("inviteCode").GetString() ?? "";
                await DisplayAlert("Grupo creado", $"Código de invitación: {inviteCode}\n\n(Código copiado al portapapeles)", "OK");
                await Clipboard.SetTextAsync(inviteCode);
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

    
    
}