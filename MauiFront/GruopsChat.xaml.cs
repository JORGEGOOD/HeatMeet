namespace MauiFront;
public partial class GroupsChat : ContentPage
{
    public GroupsChat()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSendTapped(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text))
            return;

        // Aquí irá la lógica para enviar el mensaje
        await DisplayAlert("Mensaje", $"Enviado: {MessageEntry.Text}", "OK");
        MessageEntry.Text = string.Empty;
    }

    private async void OnPropuestaTapped(object sender, EventArgs e)
    {
        await DisplayAlert("Propuesta", "Entrando en la propuesta...", "OK");
    }
}