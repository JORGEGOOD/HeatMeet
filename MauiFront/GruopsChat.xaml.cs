using System.Net.Sockets;
using System.Text.Json;
using SharedModels;
namespace MauiFront;

public partial class GroupsChat : ContentPage
{
    private bool _isChatActive;
    private int _lastMessageId;

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
        if(item.IsMessage && item.Message != null)
        {
            //we process this as a message
            AddMessage(item.Message, currentUserId);
        }
        if(item.IsEvent && item.Event != null)
        {
            //we process this as an event
            AddEventCard(item.Event);
        }
    }

    void AddMessage(MessageDto? msg, int currentUserId)//Add individual message
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

        if (msg.Id > _lastMessageId)
            _lastMessageId = msg.Id;
    }

    private void AddEventCard(EventDto ev)
    {
        // Aseguramos que la UI se actualice en el hilo principal
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

            var header = new HorizontalStackLayout { Spacing = 8 };
            header.Children.Add(new Image
            {
                Source = "calendar_icon.png", // Asegúrate de que este recurso exista
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

            var detailsRow = new HorizontalStackLayout { Spacing = 10 };
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
            votarBtn.Clicked += OnVotarClicked;

            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(header);
            stack.Children.Add(titleLabel);
            stack.Children.Add(detailsRow);
            stack.Children.Add(votarBtn);

            card.Content = stack;
            MessagesContainer.Children.Add(card);//Insert event into the messages container
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
        MessagesContainer.Children.Clear();

        //Join messages and events
        List<ChatItem> chatItems = new List<ChatItem>();
        chatItems.AddRange(messages.Select(m => new ChatItem { CreateDate = m.CreateDate, Message = m }));//add messages to list
        chatItems.AddRange(events.Select(e => new ChatItem { CreateDate = e.CreateDate, Event = e }));    //add events to list
        //Sort list
        List<ChatItem> sortedChatItems = chatItems.OrderBy(c => c.CreateDate).ToList();

        //Add each individual item
        foreach (ChatItem item in sortedChatItems) AddChatItem(item, currentUserId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MessagesContainer.ChildAdded += OnChildAddedScroll;//Maui trolls bcs it freezes the screen, so this doesnt work when spammed

        _isChatActive = true;
        Task.Run(async () => await RefreshMessagesLoop());

        string groupName = Preferences.Get("groupName", "(Grupo)");
        GroupNameLabel.Text = groupName;

        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);

        if (groupId == 0)
        {

            await DisplayAlert("Error", "Chat couldn't be loaded: groupId is 0", "OK");
            return;
        }

        try//Load messages and events
        {
            Socket socket = NetUtils.NetUtils.ConnectToServer();

            //Get messages
            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_GROUP_MESSAGES_AND_EVENTS",
                Data = new { groupId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                //get messages
                string messagesJson = data.GetProperty("messages").GetRawText();
                List<MessageDto>? messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                //get events
                List<EventDto>? events = new();
                if(data.TryGetProperty("events",out JsonElement eventsJson))
                {
                    events = JsonSerializer.Deserialize<List<EventDto>>(eventsJson.GetRawText()) ?? new();
                }
                
                LoadMessages(messages, events, userId);

                NetworkMessage ack = new NetworkMessage //TODO: get rid of this ACK garbage
                {
                    Command = "ACK",
                    Data = new { success = true }
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


    //This is a failed attempt to trick Maui into killing the screen when we get out instead of the usual 2-3sec
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isChatActive = false;
        MessagesContainer.ChildAdded -= OnChildAddedScroll;
    }
    private void OnChildAddedScroll(object? sender, ElementEventArgs e) => ScrollToBottom();




    private async void OnSendTapped(object sender, EventArgs e)//Send indivual message
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text)) return;

        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            string content = MessageEntry.Text;
            int groupId = Preferences.Get("groupId", 0);
            int userId = Preferences.Get("userId", 0);

            //Optimism chat technology --> Send content-only message and refill it when server answers details
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
                                       : "Without error details. ";
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


    private bool _isProcessingNetwork = false;
    private async Task RefreshMessagesLoop()//infinite thread of new message lookups
    {
        while (_isChatActive)
        {
            await Task.Delay(2000);

            if (_isProcessingNetwork) continue;

            Socket socket = null;
            try
            {
                _isProcessingNetwork = true;
                socket = NetUtils.NetUtils.ConnectToServer();
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

                NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    string messagesJson = data.GetProperty("messages").GetRawText();
                    List<MessageDto>? newMessages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                    if (newMessages != null && newMessages.Count > 0)
                    {
                        int maxIdreceived = newMessages.Max(m  => m.Id);
                        _lastMessageId = maxIdreceived;
                        if (maxIdreceived > _lastMessageId)
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

    // =========================================================================
    // 3 Point menú
    // =========================================================================

    private async void OnMenuTapped(object sender, EventArgs e)
    {
        string action = await DisplayActionSheet(
            "Opciones del grupo",
            "Cancelar",
            null,
            "📋 Copiar código del grupo",
            "🚪 Salir del grupo"
        );

        if (action == "📋 Copiar código del grupo")
        {
            await OnCopyGroupCode();
        }
        else if (action != null && action.Contains("Salir"))
        {
            await OnLeaveGroup();
        }
    }

    // ─── Copy code group ──────────────────────────────────────────────
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

    // ─── Group info ───────────────────────────────────────────────────────
    private async Task OnGroupInfo()
    {
        int groupId = Preferences.Get("groupId", 0);
        string groupName = Preferences.Get("groupName", "(Grupo)");
        Socket? socket = null;
        try
        {
            socket = NetUtils.NetUtils.ConnectToServer();

            var message = new NetworkMessage
            {
                Command = "GET_GROUP_INFO",
                Data = new { groupId }
            };
            NetUtils.NetUtils.SendJson(socket, message);

            var response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                int memberCount = data.GetProperty("memberCount").GetInt32();
                string? createdBy = data.GetProperty("createdBy").GetString();
                string? createdAt = data.GetProperty("createdAt").GetString();

                await DisplayAlert(
                    $"ℹ️ {groupName}",
                    $"👥 Miembros: {memberCount}\n" +
                    $"👤 Creado por: {createdBy}\n" +
                    $"📅 Fecha: {createdAt}",
                    "Cerrar");
            }
            else
            {
                // Fallback with local data if the server does not yet implement it
                await DisplayAlert($"ℹ️ {groupName}", $"ID del grupo: {groupId}", "Cerrar");
            }
        }
        catch (Exception)
        {
            await DisplayAlert($"ℹ️ {groupName}", $"ID del grupo: {groupId}", "Cerrar");
        }
        finally
        {
            if (socket != null) NetUtils.NetUtils.CloseSocket(socket);
        }
    }

    // ─── Leave the group ───────────────────────────────────────────────────
    private async Task OnLeaveGroup()
    {
        bool confirm = await DisplayAlert(
            "🚪 Salir del grupo",
            "¿Seguro que quieres salir? Ya no podrás leer los mensajes.",
            "Salir",
            "Cancelar");

        if (!confirm) return;

        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);
<<<<<<< Updated upstream

        Socket socket = null;

=======
        Socket? socket = null;
>>>>>>> Stashed changes
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

                await Navigation.PopToRootAsync();
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

    private void VotarBtn_Clicked(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
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
                await DisplayAlert("Sin eventos", "Este grupo aun no tiene eventos.", "OK");
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

    private async void OnVotarClicked(object? sender, EventArgs e)
    {
      
        await Navigation.PushAsync(new VotePage());
        
    }



}