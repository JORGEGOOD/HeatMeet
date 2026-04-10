namespace MauiFront
{
    public partial class EventDetailPage : ContentPage
    {
        public EventDetailPage(EventDto evento)
        {
            InitializeComponent();

            TituloLabel.Text = evento.Title;
            UbicacionLabel.Text = string.IsNullOrWhiteSpace(evento.Ubicacion) ? "No especificada" : evento.Ubicacion;
            DireccionLabel.Text = string.IsNullOrWhiteSpace(evento.DireccionUrl) ? "No especificada" : evento.DireccionUrl;
            FechaLabel.Text = evento.FechaHora.ToLocalTime().ToString("dd/MM/yyyy");
            HoraLabel.Text = evento.FechaHora.ToLocalTime().ToString("HH:mm");
        }
    }
}