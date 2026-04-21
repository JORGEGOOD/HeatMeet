using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        if (item.IsMessage && item.Message != null)
            AddMessage(item.Message, currentUserId);
        if (item.IsEvent && item.Event != null)
            AddEventCard(item.Event);
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

        if (msg.Id > _lastMessageId)
            _lastMessageId = msg.Id;
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

            var header = new HorizontalStackLayout { Spacing = 8 };
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
            votarBtn.Clicked += async (s, e) =>
            {
                //Set eventId on preferences
                Preferences.Set("eventId", ev.Id);
                //Goto Vote page
                await Navigation.PushAsync(new VotePage());
            };

            var stack = new VerticalStackLayout { Spacing = 6 };
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
        MessagesContainer.Children.Clear();

        List<ChatItem> chatItems = new List<ChatItem>();
        chatItems.AddRange(messages.Select(m => new ChatItem { CreateDate = m.CreateDate, Message = m }));
        chatItems.AddRange(events.Select(e => new ChatItem { CreateDate = e.CreateDate, Event = e }));
        List<ChatItem> sortedChatItems = chatItems.OrderBy(c => c.CreateDate).ToList();

        foreach (ChatItem item in sortedChatItems) AddChatItem(item, currentUserId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MessagesContainer.ChildAdded += OnChildAddedScroll;

        _isChatActive = true;
        Task.Run(async () => await RefreshMessagesLoop());

        string groupName = Preferences.Get("groupName", "(Grupo)");
        GroupNameLabel.Text = groupName;

        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);

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

        try
        {
            Socket socket = NetUtils.NetUtils.ConnectToServer();

            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_GROUP_MESSAGES_AND_EVENTS",
                Data = new { groupId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                string messagesJson = data.GetProperty("messages").GetRawText();
                List<MessageDto>? messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                List<EventDto>? events = new();
                if (data.TryGetProperty("events", out JsonElement eventsJson))
                    events = JsonSerializer.Deserialize<List<EventDto>>(eventsJson.GetRawText()) ?? new();

                LoadMessages(messages, events, userId);

                NetworkMessage ack = new NetworkMessage
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isChatActive = false;
        MessagesContainer.ChildAdded -= OnChildAddedScroll;
    }

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

    private bool _isProcessingNetwork = false;
    private async Task RefreshMessagesLoop()
    {
        while (_isChatActive)
        {
            await Task.Delay(2000);

            if (_isProcessingNetwork) continue;

            Socket? socket = null;
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
                        int maxIdreceived = newMessages.Max(m => m.Id);
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
            await OnCopyGroupCode();
        else if (action != null && action.Contains("Salir"))
            await OnLeaveGroup();
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

    private async Task OnLeaveGroup()
    {
        bool confirm = await DisplayAlert(
            "🚪 Salir del grupo",
            "¿Seguro que quieres salir? Se te elimara del grupo.",
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
}