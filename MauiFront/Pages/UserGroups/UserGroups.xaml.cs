using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public class MemberDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Inicial => string.IsNullOrEmpty(Name) ? "?" : Name[0].ToString().ToUpper();
    }

    public partial class UserGroups : ContentPage
    {
        public UserGroups()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            int groupId = Preferences.Get("groupId", 0);
            string groupName = Preferences.Get("groupName", "Grupo");

            GroupNameLabel.Text = groupName;

            if (groupId == 0) return;

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_GROUP_MEMBERS",
                    Data = new { groupId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var members = JsonSerializer.Deserialize<List<MemberDto>>(
                        data.GetProperty("members").GetRawText(), options);

                    MembersCollection.ItemsSource = members;
                    MemberCountLabel.Text = $"{members?.Count ?? 0} miembros";
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