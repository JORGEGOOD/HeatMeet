using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public class EventDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Ubicacion { get; set; }
        public string? DireccionUrl { get; set; }
        public DateTime FechaHora { get; set; }
        public int GroupId { get; set; }

        public string FechaHoraFormatted =>
            FechaHora.ToLocalTime().ToString("dd/MM/yyyy  HH:mm");
    }

    public partial class EventPage : ContentPage
    {
        public EventPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            int groupId = Preferences.Get("groupId", 0);
            if (groupId == 0) return;

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();

                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_GROUP_EVENTS",
                    Data = new { groupId }
                };

                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var eventsJson = data.GetProperty("events").GetRawText();
                    var events = JsonSerializer.Deserialize<List<EventDto>>(eventsJson);
                    EventsCollection.ItemsSource = events;
                }
                else
                {
                    EventsCollection.ItemsSource = new List<EventDto>();
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

        private async void OnEventTapped(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is EventDto ev)
                await Navigation.PushAsync(new EventDetailPage(ev));
        }
    }
}