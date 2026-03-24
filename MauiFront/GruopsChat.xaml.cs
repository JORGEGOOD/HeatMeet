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
            int userId = Preferences.Get("user_id", 0);

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
                    await DisplayAlert("ERROR", "Message could not be sent", "Ok");
                }


            }
            else
            {
                await DisplayAlert("ERROR", "Didn't received a message but anything else", "Ok");
            }
            
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
            //delete the message?
        }finally
        {
            if(socket!=null) NetUtils.NetUtils.CloseSocket(socket);
        }


    }


    //remove this entirely, or do it good enough
    private async void OnProposalTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Proposal", "Proposal clicked!", "OK");
    }
}