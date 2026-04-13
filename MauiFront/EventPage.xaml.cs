using System.Net.Sockets;
using System.Text.Json;
using SharedModels;

namespace MauiFront
{
    public partial class EventPage : ContentPage
    {
        public EventPage()
        {
            InitializeComponent();
        }

        private async void OnEventCardTapped(object sender, EventArgs e)
        {
            
            var frame = sender as Frame;

            
            if (frame?.BindingContext is EventDto ev)
            {
                
                await Navigation.PushAsync(new EventDetailPage(ev));
            }
        }


        protected override async void OnAppearing()
        {
            base.OnAppearing();
            int groupId = Preferences.Get("groupId", 0);
            int userId = Preferences.Get("userId", 0);
            if (groupId == 0) return;

            Socket socket = null;
            try
            {
                socket = NetUtils.NetUtils.ConnectToServer();
                NetworkMessage message = new NetworkMessage
                {
                    Command = "GET_USER_EVENTS_AND_AVIABILITY",
                    Data = new { userId }
                };
                NetUtils.NetUtils.SendJson(socket, message);
                NetworkMessage response = NetUtils.NetUtils.ReceiveJson<NetworkMessage>(socket);

                if (response?.Data is JsonElement data && data.GetProperty("success").GetBoolean())
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var all = JsonSerializer.Deserialize<List<EventDto>>(
                                  data.GetProperty("events").GetRawText(), options);

                    // Filtrar solo eventos reales de este grupo
                    var groupEvents = all?
                        .Where(e => e.IsEvent && e.GroupId == groupId)
                        .OrderByDescending(e => e.Date)
                        .ToList();

                    EventsCollection.ItemsSource = groupEvents ?? new List<EventDto>();
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
    }
}