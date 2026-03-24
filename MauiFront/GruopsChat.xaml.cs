using System.Net.Sockets;
using System.Text.Json;
using SharedModels;
using Socket = System.Net.Sockets.Socket;
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
        public int UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreateDate { get; set; }
        public int userId { get; set; }
        public string UserName { get; set; }
    }


    void LoadMessages(List<MessageDto> mensajes, int currentUserId)
    {
        MessagesContainer.Children.Clear();//Clears residual messages from another group chat

        foreach (MessageDto? msg in mensajes)
        {
            int senderId = msg.UserId != 0 ? msg.UserId : msg.userId;
            bool isMine = senderId == currentUserId;

            // Top row: Name | Date and time 
            var headerGrid = new Grid
            {
                ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
                Margin = new Thickness(2, 0, 2, 3)
            };

            // Name 
            var nameLabel = new Label //And this loads the content of the frame
            {
                Text = isMine ? "Tú" : (msg.UserName ?? "Usuario"),
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#FF6A00"),
                HorizontalOptions = LayoutOptions.Start
            };

            // Date and time (right)
            var dateLabel = new Label
            {
                Text = msg.CreateDate.ToLocalTime().ToString("dd/MM  HH:mm"),
                FontSize = 11,
                TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#9AAABB"),
                HorizontalOptions = LayoutOptions.End
            };

            headerGrid.Add(nameLabel, 0, 0);
            headerGrid.Add(dateLabel, 1, 0);

            // Message content
            var contentLabel = new Label
            {
                Text = msg.Content,
                FontSize = 14,
                TextColor = isMine ? Colors.White : Color.FromArgb("#222")
            };

            // content Cheader + mensaje 
            var bubble = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { headerGrid, contentLabel }
            };

            var frame = new Frame //This loads all the css from the message
            {
                BackgroundColor = isMine ? Color.FromArgb("#2C3E6B") : Colors.White,
                CornerRadius = 16,
                Padding = new Thickness(12, 8),
                HorizontalOptions = isMine ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 260,
                Content = bubble
            };

            MessagesContainer.Children.Add(frame);
        }
    }

    //Get group info and messages and update the page
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        string groupName = Preferences.Get("groupName", "(Grupo)");
        GroupNameLabel.Text = groupName;

        int groupId = Preferences.Get("groupId", 0);

        int userId = Preferences.Get("userId", 0);

        if (groupId == 0)
        {   await DisplayAlert("Error", "Chat couldn't be loaded ", "OK");
            await DisplayAlert("DEBUG", $"groupId read: {groupId}", "OK");  /*return;*/
        }

        try
        {
            //connect to server
            Socket? socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);

            //build json
            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_GROUP_MESSAGES",
                Data = new { groupId }
            };
            
            //send command
            NetUtils.NetUtils.SendJson(socket, message);
            
            //receive messages
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            //if message is sucess
            if (response.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                var messagesJson = data.GetProperty("messages").GetRawText();

                var messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);
                messages = messages.OrderBy(m => m.CreateDate).ToList();

                LoadMessages(messages, userId);

                //send ok to server
                NetworkMessage ack = new NetworkMessage
                {
                    Command = "ACK",
                    Data = new {success = true}
                };
                NetUtils.NetUtils.SendJson(socket, ack);
            }

            NetUtils.NetUtils.CloseSocket(socket);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }



    //Send message
    private async void OnSendTapped(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text)) return;

        //----Update screen----

        //put message on screen and auto generate "CreateDate = DateTime.UtcNow"



        //---------------------

        Socket? socket = null;
        try
        {
            //connect to server
            socket = NetUtils.NetUtils.CreateClientSocket("10.0.2.2", 8888);

            //servermessage logic

            //get data
            string content = MessageEntry.Text;
            int groupId = Preferences.Get("groupId", 0);
            int userId = Preferences.Get("userId", 0);

            //build json
            NetworkMessage message = new NetworkMessage
            {
                Command = "SEND_CHAT_MESSAGE",
                Data = new
                {
                    content = content,
                    groupId = groupId,
                    userId = userId
                }
            };

            await DisplayAlert("Comprobacion",$"DEBUG: Enviando mensaje con UserId: {userId}","Ok");

            //send json
            NetUtils.NetUtils.SendJson(socket, message);

            //receive ok
            NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);
            if (response.Data is JsonElement data)
            {
                if(data.GetProperty("success").GetBoolean())
                {
                    //message is success
                    //do nothing
                }
                else
                {
                    // ERROR LÓGICO: El servidor respondió, pero success es false
                    // Intentamos leer el mensaje de error que envía el servidor
                    string serverMsg = data.TryGetProperty("message", out JsonElement msgProp)
                                       ? msgProp.GetString()
                                       : "No error details provided";

                    await DisplayAlert("ERROR SERVIDOR", serverMsg, "Ok");
                }


            }
            else
            {
                // ERROR DE FORMATO: Lo que llegó no es un JsonElement o la respuesta es nula
                string rawResponse = response?.Data?.ToString() ?? "null";
                await DisplayAlert("ERROR FORMATO", $"Recibido algo inesperado: {rawResponse}", "Ok");
            }
            
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
            //delete the message?
        }finally
        {
            if(socket!=null) NetUtils.NetUtils.CloseSocket(socket);
            MessageEntry.Text = "";
        }


    }


    //remove this entirely, or do it good enough
    private async void OnProposalTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Proposal", "Proposal clicked!", "OK");
    }
}