namespace MauiFront
{
    public partial class EventDetailPage : ContentPage
    {
        public EventDetailPage(EventDto evento)
        {
            InitializeComponent();

            TituloLabel.Text = evento.Title;
            UbicacionLabel.Text = string.IsNullOrWhiteSpace(evento.Location) ? "No especificada" : evento.Location;
            DireccionLabel.Text = string.IsNullOrWhiteSpace(evento.AddressUrl) ? "No especificada" : evento.Location;
            FechaLabel.Text = evento.Date.ToLocalTime().ToString("dd/MM/yyyy");
            HoraLabel.Text = evento.Date.ToLocalTime().ToString("HH:mm");
        }
    }
}