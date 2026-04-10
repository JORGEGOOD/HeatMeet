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

    public class MessageDto
    {
        public int Id { get; set; }
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

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(2, 0, 2, 3)
        };

        var nameLabel = new Label
        {
            Text = isMine ? "Tú " : (msg.UserName ?? "Usuario"),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#FF6A00"),
            HorizontalOptions = LayoutOptions.Start
        };

        var dateLabel = new Label
        {
            Text = msg.CreateDate.ToLocalTime().ToString("dd/MM  HH:mm"),
            FontSize = 11,
            TextColor = isMine ? Color.FromArgb("#8BA3D4") : Color.FromArgb("#9AAABB"),
            HorizontalOptions = LayoutOptions.End
        };

        headerGrid.Add(nameLabel, 0, 0);
        headerGrid.Add(dateLabel, 1, 0);

        var contentLabel = new Label
        {
            Text = msg.Content,
            FontSize = 14,
            TextColor = isMine ? Colors.White : Color.FromArgb("#222")
        };

        var bubble = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { headerGrid, contentLabel }
        };

        var frame = new Frame
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

        ScrollToBottom();
    }

    private async void ScrollToBottom()
    {
        await Task.Delay(50);
        await ChatScrollView.ScrollToAsync(MessagesContainer, ScrollToPosition.End, false);
    }

    void LoadMessages(List<MessageDto> mensajes, int currentUserId)
    {
        MessagesContainer.Children.Clear();
        foreach (MessageDto? msg in mensajes)
            AddMessage(msg, currentUserId);
        ScrollToBottom();
    }
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
        {
            await DisplayAlert("Error", "Chat couldn't be loaded", "OK");
            return;
        }

        try
        {
            Socket socket = NetUtils.NetUtils.ConnectToServer();

            NetworkMessage message = new NetworkMessage
            {
                Command = "GET_GROUP_MESSAGES",
                Data = new { groupId }
            };

            NetUtils.NetUtils.SendJson(socket, message);
            NetworkMessage? response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                var messagesJson = data.GetProperty("messages").GetRawText();
                var messages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

                messages = messages.OrderBy(m => m.CreateDate).ToList();
                LoadMessages(messages, userId);

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

            //Optimism chat technology --> Creates content-only message and refill when server sends details
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
            NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

            if (response.Data is JsonElement data)
            {
                if (data.GetProperty("success").GetBoolean())
                {
                    if (data.TryGetProperty("newId", out JsonElement idProp))
                        _lastMessageId = idProp.GetInt32();
                }
                else
                {
                    string serverMsg = data.TryGetProperty("message", out JsonElement msgProp)
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

                NetUtils.NetUtils.SendJson(socket, message);
                var response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var messagesJson = data.GetProperty("messages").GetRawText();
                    var newMessages = JsonSerializer.Deserialize<List<MessageDto>>(messagesJson);

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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isChatActive = false;
    }





    // =========================================================================
    // 3 Point menú
    // =========================================================================

    private async void OnMenuTapped(object sender, EventArgs e)
    {
        //Dynamic mute button label based on current preference
        int groupId = Preferences.Get("groupId", 0);
        bool isMuted = Preferences.Get($"muted_group_{groupId}", false);
        string muteText = isMuted ? "🔔 Activar notificaciones" : "🔕 Silenciar notificaciones";

        string action = await DisplayActionSheet
        (
            "Opciones del grupo",
            "Cancelar",
            null,
            "📋 Copiar código del grupo",
            "ℹ️  Info del grupo",
            muteText,
            "🚪 Salir del grupo"
        );

        switch (action)
        {
            case "📋 Copiar código del grupo":
                await OnCopyGroupCode();
                break;
            case "ℹ️  Info del grupo":
                await OnGroupInfo();
                break;
            case "🔕 Silenciar notificaciones":
            case "🔔 Activar notificaciones":
                OnMuteToggle();
                break;
            case "🚪 Salir del grupo":
                await OnLeaveGroup();
                break;
        }
    }

    // ─── Copy code group ──────────────────────────────────────────────
    private async Task OnCopyGroupCode()
    {
        int groupId = Preferences.Get("groupId", 0);
        Socket socket = null;
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
                string serverMsg = response?.Data is JsonElement d2 &&
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
        Socket socket = null;
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
                string createdBy = data.GetProperty("createdBy").GetString();
                string createdAt = data.GetProperty("createdAt").GetString();

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

    // ─── Mute / Unmute notifications (local preference) ──────────────
    private void OnMuteToggle()
    {
        int groupId = Preferences.Get("groupId", 0);
        string key = $"muted_group_{groupId}";
        bool isMuted = Preferences.Get(key, false);

        Preferences.Set(key, !isMuted);

        string msg = !isMuted
            ? "🔕 Notificaciones silenciadas para este grupo."
            : "🔔 Notificaciones activadas para este grupo.";

        MainThread.BeginInvokeOnMainThread(async () =>
            await DisplayAlert("Notificaciones", msg, "OK"));
    }

    // ─── Leave the group ───────────────────────────────────────────────────
    private async Task OnLeaveGroup()
    {
        bool confirm = await DisplayAlert(
            "🚪 Salir del grupo",
            "¿Seguro que quieres salir? Ya no podrás leer los mensajes de este grupo.",
            "Salir",
            "Cancelar");

        if (!confirm) return;

        int groupId = Preferences.Get("groupId", 0);
        int userId = Preferences.Get("userId", 0);
        Socket socket = null;
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

            if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
            {
                _isChatActive = false; //stops the refresh loop
                await DisplayAlert("👋", "Has salido del grupo.", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                string serverMsg = response?.Data is JsonElement d2 &&
                                   d2.TryGetProperty("message", out JsonElement mp)
                                   ? mp.GetString() : "No se pudo salir del grupo.";
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

    private async void PageVote(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new VotePage());
    }

}