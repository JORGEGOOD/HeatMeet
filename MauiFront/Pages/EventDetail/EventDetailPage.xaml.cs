namespace MauiFront
{
    public partial class EventDetailPage : ContentPage
    {
        public EventDetailPage(EventDto evento)
        {
            InitializeComponent();

            TituloLabel.Text = evento.Title;
            DireccionLabel.Text = string.IsNullOrWhiteSpace(evento.AddressUrl) ? "No especificada" : evento.Location;
            DireccionLabel.Text = string.IsNullOrWhiteSpace(evento.AddressUrl) ? "No especificada" : evento.AddressUrl;
            FechaLabel.Text = evento.Date.ToLocalTime().ToString("dd/MM/yyyy");
            HoraLabel.Text = evento.Date.ToLocalTime().ToString("HH:mm");
        }
    }
}