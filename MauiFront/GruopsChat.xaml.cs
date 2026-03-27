using System.Net.Sockets;
using System.Text.Json;
using SharedModels;
using Socket = System.Net.Sockets.Socket;
namespace MauiFront;
public partial class GroupsChat : ContentPage
{
    //generals
    private bool _isChatActive;//for the clock
    private int _lastMessageId;


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
        public int Id {  get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreateDate { get; set; }
        public int userId { get; set; }
        public string UserName { get; set; }
    }

    void AddMessage(MessageDto? msg, int currentUserId)
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

        //update _lastMessageDate
        if(msg.Id > _lastMessageId)
        {
            _lastMessageId = msg.Id;
        }

        ScrollToBottom();

    }

    private async void ScrollToBottom()
    {
        // Esperamos un instante a que el layout se actualice con el nuevo mensaje
        await Task.Delay(50);
        await ChatScrollView.ScrollToAsync(MessagesContainer, ScrollToPosition.End, false);
    }


    void LoadMessages(List<MessageDto> mensajes, int currentUserId)
    {
        MessagesContainer.Children.Clear();//Clears residual messages from another group chat

        foreach (MessageDto? msg in mensajes)
        {
           AddMessage(msg, currentUserId);
        }
        ScrollToBottom();
    }

    //Get group info and messages and update the page
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isChatActive = true;
        Task.Run(async () => await RefreshMessagesLoop());


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
            Socket socket = NetUtils.NetUtils.ConnectToServer();

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
                var messagesJson = data.GetProperty("messages").GetRawText();//get messages to raw text
                var messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson); //convert to raw text 

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


        Socket? socket = null;
        try
        {
            //connect to server
            socket = NetUtils.NetUtils.ConnectToServer();

            //servermessage logic

            //get data
            string content = MessageEntry.Text;
            int groupId = Preferences.Get("groupId", 0);
            int userId = Preferences.Get("userId", 0);


            MessageDto messageFront = new MessageDto
            {
                Id = 0, //<-- Only for the optimistic message
                Content = content,
                UserId = userId,
                UserName = "Tú",
                CreateDate = DateTime.Now
            };

            AddMessage(messageFront, userId);


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
                    if(data.TryGetProperty("newId",out JsonElement idProp))
                    {
                        _lastMessageId = idProp.GetInt32();
                    }
                }
                else
                {
                    //Extended json error
                    string serverMsg = data.TryGetProperty("message", out JsonElement msgProp)
                                       ? msgProp.GetString()
                                       : "No error details provided";

                    await DisplayAlert("ERROR SERVER", serverMsg, "Ok");
                }
            }
            else
            {
                //Extended format error
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

    private bool _isProcessingNetwork = false;
    private async Task RefreshMessagesLoop()
    {
        while (_isChatActive)
        {
            await Task.Delay(2000); // Un respiro entre peticiones

            if (_isProcessingNetwork) continue;

            Socket socket = null;
            try
            {
                _isProcessingNetwork = true;
                socket = NetUtils.NetUtils.ConnectToServer(); // Usa tu método estándar

                if (socket == null) continue;

                var message = new NetworkMessage
                {
                    Command = "RELOAD_CHAT_MESSAGES",
                    Data = new
                    {
                        groupId = Preferences.Get("groupId", 0),
                        lastId = _lastMessageId
                    }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                var response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var messagesJson = data.GetProperty("messages").GetRawText();
                    var newMessages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                    if (newMessages != null && newMessages.Count > 0)
                    {
                        int currentUserId = Preferences.Get("userId", 0);
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            foreach (var msg in newMessages.OrderBy(m => m.Id))
                            {
                                if (msg.Id > _lastMessageId)
                                    AddMessage(msg, currentUserId);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh Error: {ex.Message}");
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
                _isProcessingNetwork = false;
            }
        }
    }




    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isChatActive = false;
    }



    //remove this entirely, or do it good enough
    private async void OnProposalTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Proposal", "Proposal clicked!", "OK");
    }
}


