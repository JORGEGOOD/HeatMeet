using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;
using SharedModels;
namespace MauiFront;

public partial class GroupsChat : ContentPage
{
    private bool _isChatActive;
    private int _lastMessageId;
    private bool _isInitialLoadComplete;


    public GroupsChat()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    void AddChatItem(ChatItem item, int currentUserId)
    {
        int itemId = item.IsMessage ? item.Message.Id : item.Event.Id;
        if (itemId <= _lastMessageId && _lastMessageId != 0) return;

        if (item.IsMessage && item.Message != null) AddMessage(item.Message, currentUserId);
        if (item.IsEvent   && item.Event   != null) AddEventCard(item.Event);

        //update AGAIN lastMessageId
        if (itemId > _lastMessageId) _lastMessageId = itemId;
    }

    void AddMessage(MessageDto? msg, int currentUserId)
    {
        int senderId = msg.UserId != 0 ? msg.UserId : msg.userId;
        bool isMine = senderId == currentUserId;

        Grid? headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(2, 0, 2, 3)
        };

        Label? nameLabel = new Label
        {
            Text = isMine ? "Tú " : (msg.UserName ?? "Usuario"),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#FF6A00"),
            HorizontalOptions = LayoutOptions.Start
        };

        Label? dateLabel = new Label
        {
            Text = msg.CreateDate.ToLocalTime().ToString("dd/MM  HH:mm"),
            FontSize = 11,
            TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#9AAABB"),
            HorizontalOptions = LayoutOptions.End
        };

        headerGrid.Add(nameLabel, 0, 0);
        headerGrid.Add(dateLabel, 1, 0);

        Label? contentLabel = new Label
        {
            Text = msg.Content,
            FontSize = 14,
            TextColor = isMine ? Colors.White : Color.FromArgb("#222")
        };

        VerticalStackLayout? bubble = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { headerGrid, contentLabel }
        };

        Frame? frame = new Frame
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

    private void AddEventCard(EventDto ev)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            string formattedDate = ev.Date.ToLocalTime().ToString("dd/MM/yyyy  HH:mm");

            Frame card = new()
            {
                CornerRadius = 12,
                BorderColor = Color.FromArgb("#FF8C00"),
                BackgroundColor = Colors.White,
                Padding = new Thickness(12, 10),
                Margin = new Thickness(20, 4),
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Fill
            };

            HorizontalStackLayout header = new() { Spacing = 8 };
            header.Children.Add(new Image
            {
                Source = "calendar_icon.png",
                WidthRequest = 18,
                HeightRequest = 18,
                VerticalOptions = LayoutOptions.Center
            });
            header.Children.Add(new Label
            {
                Text = "Propuesta",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF6A00"),
                VerticalOptions = LayoutOptions.Center
            });

            Label titleLabel = new()
            {
                Text = ev.Title,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222")
            };

            HorizontalStackLayout detailsRow = new(){ Spacing = 10 };
            detailsRow.Children.Add(new Label
            {
                Text = "📅 " + formattedDate,
                FontSize = 12,
                TextColor = Colors.Gray
            });

            if (!string.IsNullOrWhiteSpace(ev.Location))
            {
                detailsRow.Children.Add(new Label
                {
                    Text = "📍 " + ev.Location,
                    FontSize = 12,
                    TextColor = Colors.Gray
                });
            }

            Button votarBtn = new()
            {
                Text = "Votar",
                BackgroundColor = Color.FromArgb("#FF6A00"),
                TextColor = Colors.White,
                CornerRadius = 20,
                HeightRequest = 38,
                HorizontalOptions = LayoutOptions.Fill,
                FontSize = 13
            };

            votarBtn.ClassId = ev.Id.ToString();
            votarBtn.Clicked += async (s, e) =>
            {
                Preferences.Set("eventId", ev.Id);
                await Navigation.PushAsync(new VotePage());
            };

            VerticalStackLayout stack = new(){ Spacing = 6 };
            stack.Children.Add(header);
            stack.Children.Add(titleLabel);
            stack.Children.Add(detailsRow);
            stack.Children.Add(votarBtn);

            card.Content = stack;
            MessagesContainer.Children.Add(card);
        });
    }

    private async void ScrollToBottom()
    {
        await Task.Delay(100);
        await ChatScrollView.ScrollToAsync(MessagesContainer, ScrollToPosition.End, false);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ChatScrollView.ScrollToAsync(MessagesContainer, ScrollToPosition.End, false);
        });
    }

    void LoadMessages(List<MessageDto> messages, List<EventDto> events, int currentUserId)
    {
        //Clear chat
        MessagesContainer.Children.Clear();

        //Get messages and events into a list
        List<ChatItem> chatItems = new List<ChatItem>();
        chatItems.AddRange(messages.Select(m => new ChatItem { CreateDate = m.CreateDate, Message = m }));//Put in messages
        chatItems.AddRange(events.Select(e => new ChatItem { CreateDate = e.CreateDate, Event = e }));//Put in events
        List<ChatItem> sortedChatItems = chatItems.OrderBy(c => c.CreateDate).ToList();//Sort list

        //Print each item and update lastMessageId, because Android doesn't know better
        int maxId = 0;
        foreach (var item in sortedChatItems)
        {
            //put message
            AddChatItem(item, currentUserId);

            //update maxId
            int itemId = item.IsMessage ? item.Message.Id : item.Event.Id;
            if (itemId > maxId) maxId = itemId;
        }

        // ACTUALIZACIÓN CRUCIAL
        _lastMessageId = maxId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        //Subscribe chat variable to XAML container
        MessagesContainer.ChildAdded += OnChildAddedScroll;

        _isChatActive = true;
        _isInitialLoadComplete = false;

        //Get info from user and group
        string groupName = Preferences.Get("groupName", "(Grupo)");
        GroupNameLabel.Text = groupName;
        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);

        //Load image
        string savedAvatar = Preferences.Get($"groupAvatar_{groupId}", "");
        if (!string.IsNullOrEmpty(savedAvatar) && File.Exists(savedAvatar))
            GroupAvatarImage.Source = ImageSource.FromFile(savedAvatar);
        else
            GroupAvatarImage.Source = "group_avatar.svg";

        if (groupId == 0)
        {
            await DisplayAlert("Error", "Chat couldn't be loaded: groupId is 0", "OK");
            return;
        }

        //Get messages
        await LoadInitialChat();
        _isInitialLoadComplete = true;
        _ = RefreshMessagesLoop();// "_" is for the compiler to shut up and ignore warnings, else gives multiple warnings
    }

    private async Task LoadInitialChat()//Separate thread because stupid Android cannot hold networking and ui in the same place
    {
        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();
            //Send command
            NetworkMessage message = new()
            {
                Command = "GET_GROUP_MESSAGES_AND_EVENTS",
                Data    = new { groupId = Preferences.Get("groupId", 0) }
            };
            NetUtils.NetUtils.SendJson(socket, message);
            //Answer
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);
            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                //get messages and events
                List<MessageDto>? messages = JsonSerializer.Deserialize<List<MessageDto>>(data.GetProperty("messages").GetRawText());
                List<EventDto>?   events   = data.TryGetProperty("events", out var evJson)
                    ? JsonSerializer.Deserialize<List<EventDto>>(evJson.GetRawText())
                    : new List<EventDto>();

                //Load everything
                //Await because android trolls with networking
                await MainThread.InvokeOnMainThreadAsync(() => 
                {
                    LoadMessages(messages, events, Preferences.Get("userId", 0));
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inicial: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isChatActive = false;
        MessagesContainer.ChildAdded -= OnChildAddedScroll;//<--Clears chat, but android still screws it up
    }

    //Subscribe on added something to scroll bottom, but android do what he wants and this doesn't work
    private void OnChildAddedScroll(object? sender, ElementEventArgs e) => ScrollToBottom();

    private async void OnAvatarTapped(object sender, EventArgs e)
    {
        try
        {
            string action = await DisplayActionSheet(
                "Foto del grupo",
                "Cancelar",
                null,
                "📷 Hacer foto",
                "🖼️ Elegir de galería",
                "🗑️ Quitar imagen");

            FileResult? result = null;

            if (action == "📷 Hacer foto")
                result = await MediaPicker.Default.CapturePhotoAsync();
            else if (action == "🖼️ Elegir de galería")
                result = await MediaPicker.Default.PickPhotoAsync();
            else if (action == "🗑️ Quitar imagen")
            {
                int gId = Preferences.Get("groupId", 0);
                Preferences.Remove($"groupAvatar_{gId}");
                GroupAvatarImage.Source = "group_avatar.svg";
                return;
            }

            if (result != null)
            {
                int groupId = Preferences.Get("groupId", 0);
                string localPath = Path.Combine(
                    FileSystem.AppDataDirectory,
                    $"avatar_group_{groupId}.jpg");

                using Stream sourceStream = await result.OpenReadAsync();
                using FileStream destStream = File.Create(localPath);
                await sourceStream.CopyToAsync(destStream);

                Preferences.Set($"groupAvatar_{groupId}", localPath);
                GroupAvatarImage.Source = ImageSource.FromFile(localPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSendTapped(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text)) return;

        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            string content = MessageEntry.Text;
            int groupId = Preferences.Get("groupId", 0);
            int userId = Preferences.Get("userId", 0);

            MessageDto messageFront = new MessageDto
            {
                Id = 0,
                Content = content,
                UserId = userId,
                UserName = "Tú",
                CreateDate = DateTime.Now
            };
            AddMessage(messageFront, userId);

            NetworkMessage message = new NetworkMessage
            {
                Command = "SEND_CHAT_MESSAGE",
                Data = new { content, groupId, userId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response.Data is JsonElement data)
            {
                if (data.GetProperty("success").GetBoolean())
                {
                    if (data.TryGetProperty("newId", out JsonElement idProp))
                        _lastMessageId = idProp.GetInt32();
                }
                else
                {
                    string? serverMsg = data.TryGetProperty("message", out JsonElement msgProp)
                                       ? msgProp.GetString()
                                       : "Without error details.";
                    await DisplayAlert("Server error: ", serverMsg, "OK");
                }
            }
            else
            {
                string rawResponse = response?.Data?.ToString() ?? "null";
                await DisplayAlert("Format error", $"Unexpected error: {rawResponse}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
            MessageEntry.Text = "";
        }
    }

    //-- RELOAD MESSAGES --//
    private bool _isProcessingNetwork = false;
    private async Task RefreshMessagesLoop()
    {
        while (_isChatActive)
        {
            await Task.Delay(2000);

            //if nothing rare or obscure is happening
            if (!_isInitialLoadComplete || _isProcessingNetwork) continue;

            Socket? socket = null;
            try
            {
                _isProcessingNetwork = true;
                socket = NetUtils.NetUtils.ConnectToServer();
                
                if (socket == null) continue;//Is probable that this func errors due to its frecuency, so --> continue
                
                NetworkMessage? message = new()
                {
                    Command = "RELOAD_CHAT_MESSAGES",
                    Data = new
                    {
                        groupId = Preferences.Get("groupId", 0),
                        lastId = _lastMessageId
                    }
                };
                //send command
                NetUtils.NetUtils.SendJson(socket, message);

                //response
                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);
                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    //Get messages
                    List<MessageDto>? newMessages = JsonSerializer.Deserialize<List<MessageDto>>(data.GetProperty("messages").GetRawText());
                    //Get events
                    List<EventDto>?   newEvents   = JsonSerializer.Deserialize<List<EventDto>>(data.GetProperty("events").GetRawText());

                    if (newMessages?.Count > 0 || newEvents?.Count > 0)
                    {
                        //Unify both into chatItems
                        List<ChatItem> newItems = new();
                        if(newMessages!=null) newItems.AddRange(newMessages.Select(m=>new ChatItem { CreateDate = m.CreateDate, Message = m }));
                        if(newEvents!=null)   newItems.AddRange(newEvents  .Select(e=>new ChatItem { CreateDate = e.CreateDate, Event   = e }));

                        //Sort list by date
                        List<ChatItem> sortedItems = newItems.OrderBy(i => i.CreateDate).ToList();


                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            foreach (var item in sortedItems)
                            {
                                //check duplicates
                                int currentId = item.IsMessage ? item.Message.Id : item.Event.Id;
                                //add only if newer than local last
                                if (currentId > _lastMessageId)
                                {
                                    AddChatItem(item, Preferences.Get("userId", 0));
                                    _lastMessageId = currentId;
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",$"Refresh Error: {ex.Message}","Ok");
            }
            finally
            {
                if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
                _isProcessingNetwork = false;
            }
        }
    }




    //-- Less important functions --//


    private async void OnMenuTapped(object sender, EventArgs e)
    {
        string action = await DisplayActionSheet(
            "Opciones del grupo",
            "Cancelar",
            null,
            "✏️ Renombrar grupo",
            "📋 Copiar código del grupo",
            "🚪 Salir del grupo"
        );

        if (action == "✏️ Renombrar grupo")
            await OnRenameGroup();
        else if (action == "📋 Copiar código del grupo")
            await OnCopyGroupCode();
        else if (action != null && action.Contains("Salir"))
            await OnLeaveGroup();
    }

    private async Task OnRenameGroup()
    {
        string currentName = Preferences.Get("groupName", "");

        string? newName = await DisplayPromptAsync(
            "Renombrar grupo",
            "Escribe el nuevo nombre:",
            initialValue: currentName,
            maxLength: 50,
            placeholder: "Nombre del grupo");

        if (string.IsNullOrWhiteSpace(newName) || newName == currentName)
            return;

        int groupId = Preferences.Get("groupId", 0);
        Socket? socket = null;

        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            NetworkMessage message = new NetworkMessage
            {
                Command = "RENAME_GROUP",
                Data = new { groupId, newName = newName.Trim() }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data != null)
            {

                JsonElement data = (JsonElement)response.Data;

                if (data.TryGetProperty("success", out JsonElement successElem) && successElem.GetBoolean())
                {
                    string saved = data.TryGetProperty("newName", out JsonElement nameElem)
                        ? nameElem.GetString() ?? newName.Trim()
                        : newName.Trim();

                    Preferences.Set("groupName", saved);
                    GroupNameLabel.Text = saved;
                    await DisplayAlert("✅ Éxito", $"Grupo renombrado a \"{saved}\".", "OK");
                }
                else
                {
                    string errorMsg = data.TryGetProperty("message", out JsonElement errorElem)
                        ? errorElem.GetString() ?? "Error desconocido"
                        : "No se pudo renombrar el grupo.";

                    await DisplayAlert("❌ Error", errorMsg, "OK");
                }
            }
            else
            {
                await DisplayAlert("❌ Error", "No se recibió respuesta del servidor", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error de conexión", ex.Message, "OK");
        }
        finally
        {
            if (socket != null && socket.Connected)
                NetUtils.NetUtils.CloseSocket(socket);
        }
    }

    private async Task OnCopyGroupCode()
    {
        int groupId = Preferences.Get("groupId", 0);
        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            var message = new NetworkMessage
            {
                Command = "GET_GROUP_CODE",
                Data = new { groupId }
            };
            NetUtils.NetUtils.SendJson(socket, message);

            var response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                string code = data.GetProperty("inviteCode").GetString();
                await Clipboard.Default.SetTextAsync(code);
                await DisplayAlert("✅ Código copiado",
                    $"Código del grupo:\n\n{code}\n\nYa está en tu portapapeles.", "OK");
            }
            else
            {
                string? serverMsg = response?.Data is JsonElement d2 &&
                                   d2.TryGetProperty("message", out JsonElement mp)
                                   ? mp.GetString() : "No se pudo obtener el código.";
                await DisplayAlert("Error", serverMsg, "OK");
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

    private async Task OnLeaveGroup()
    {
        bool confirm = await DisplayAlert(
            "🚪 Salir del grupo",
            "¿Seguro que quieres salir? Se te eliminará del grupo.",
            "Salir",
            "Cancelar");

        if (!confirm) return;

        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);

        Socket? socket = null;

        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            var message = new NetworkMessage
            {
                Command = "LEAVE_GROUP",
                Data = new { groupId, userId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            var response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            System.Diagnostics.Debug.WriteLine(response?.Data?.ToString());

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                _isChatActive = false;

                Preferences.Remove("groupId");
                Preferences.Remove("groupName");

                await DisplayAlert("👋", "Has salido del grupo.", "OK");

                var stack = Navigation.NavigationStack.ToList();
                foreach (var page in stack)
                {
                    if (page is CreateGroupPage || page is JoinGroups)
                        Navigation.RemovePage(page);
                }

                await Navigation.PopAsync();
            }
            else
            {
                string? serverMsg = response?.Data is JsonElement d2 &&
                                   d2.TryGetProperty("message", out JsonElement mp)
                                   ? mp.GetString()
                                   : "No se pudo salir del grupo.";

                await DisplayAlert("Error", serverMsg, "OK");
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



    private async void OnLastEventTapped(object sender, EventArgs e)
    {
        int groupId = Preferences.Get("groupId", 0);
        if (groupId == 0) return;

        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_LAST_EVENT",
                Data = new { groupId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                int loadedEventId = data.GetProperty("eventId").GetInt32();
                Preferences.Set("eventId", loadedEventId);

                var ev = new EventDto
                {
                    Title = data.GetProperty("title").GetString() ?? "",
                    Location = data.TryGetProperty("ubicacion", out var u) ? u.GetString() : null,
                    AddressUrl = data.TryGetProperty("direccionUrl", out var d) ? d.GetString() : null,
                    Date = data.GetProperty("fechaHora").GetDateTime()
                };

                await Navigation.PushAsync(new EventDetailPage(ev));
            }
            else
            {
                await DisplayAlert("Sin eventos", "Este grupo aún no tiene eventos.", "OK");
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

    private async void OnViewAllEventsTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new EventPage());
    }

    private async void NewEvent(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewEventPage());
    }
}