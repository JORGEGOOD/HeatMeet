using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;

namespace MauiFront
{

    public partial class JoinGroups : ContentPage
    {
        public JoinGroups()
        {
            InitializeComponent();
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
                Socket socket = NetUtils.NetUtils.ConnectToServer();
                SharedModels.NetworkMessage message = new SharedModels.NetworkMessage
                {
                    Command = "JOIN_GROUP",
                    Data = new { inviteCode = GroupCodeEntry.Text.Trim().ToUpper(), userId }
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
                    string groupName = data.GetProperty("groupName").GetString();

                    Preferences.Set("groupId", groupId);
                    Preferences.Set("groupName", groupName);

                    await Navigation.PushAsync(new GroupsChat());
                }

                else
                    await DisplayAlert("Error", msg, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "No se pudo conectar al servidor: " + ex.Message, "OK");
            }
        }
    }
}