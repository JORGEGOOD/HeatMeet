using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront;
public partial class GroupsChat : ContentPage
{
    public GroupsChat()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }


    public class MessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime CreateDate { get; set; }
        public int userId { get; set; }
        public string userName { get; set; }
    }


    void LoadMessages(List<MessageDto> mensajes, int currentUserId)
    {
        MessagesContainer.Children.Clear();//Clears residual messages from another group chat

        foreach (MessageDto? msg in mensajes)
        {
            bool isMine = msg.userId == currentUserId;

            var frame = new Frame //This loads all the css from the message
            {
                BackgroundColor = isMine ? Color.FromArgb("#2C3E6B") : Colors.White,
                CornerRadius = 16,
                Padding = new Thickness(12, 8),
                HorizontalOptions = isMine ? LayoutOptions.End : LayoutOptions.Start,//is message is from user on the right
                MaximumWidthRequest = 260
            };

            Label? label = new Label//And this loads the content of the frame
            {
                Text = msg.Content,
                FontSize = 14,
                TextColor = isMine ? Colors.White : Color.FromArgb("#222")
            };

            frame.Content = label;

            MessagesContainer.Children.Add(frame);
        }
    }


    //Get group info and messages and update the page
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        int groupId = Preferences.Get("groupId", 0);
        int userId  = Preferences.Get("user_id", 0);
        if (groupId == 0){ await DisplayAlert("Error", "Chat couldn't be loaded ", "OK");  return; }

        try
        {
            Socket? socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);

            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_GROUP_MESSAGES",
                Data = new { groupId }
            };

            NetUtils.NetUtils.SendJson(socket, message);

            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);
            NetUtils.NetUtils.CloseSocket(socket);

            //if message is sucess
            if (response.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                var messagesJson = data.GetProperty("messages").GetRawText();

                var messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                LoadMessages(messages, userId);

                //send ok to server
                NetworkMessage ack = new NetworkMessage
                {
                    Command = "ACK",
                    Data = new {success = true}
                };
                NetUtils.NetUtils.SendJson(socket, ack);
            }


        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }



    //Send message
    private async void OnSendTapped(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text))
            return;

        //message logic
        await DisplayAlert("Mensaje", $"EN PROGRESO", "OK");
        MessageEntry.Text = string.Empty;
    }


    //quitar esto entero, hacerlo bien o ponerlo en ingles
    private async void OnPropuestaTapped(object sender, EventArgs e)
    {
        //await DisplayAlert("Propuesta", "Entrado en propuesta", "OK");
    }
}