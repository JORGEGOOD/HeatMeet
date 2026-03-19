namespace MauiFront;

public partial class CreateGroupPage : ContentPage
{
    public CreateGroupPage()
    {
        InitializeComponent();
    }

    private async void OnCreateGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupNameEntry.Text) ||
            string.IsNullOrWhiteSpace(AdminNameEntry.Text))
        {
            await DisplayAlert("Error", "Por favor rellena todos los campos", "OK");
            return;
        }

        // Aquí irá la lógica real de creación de grupo
        await Navigation.PushAsync(new GroupsChat());
    }

    private async void OnJoinGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupCodeEntry.Text) ||
            string.IsNullOrWhiteSpace(MemberNameEntry.Text))
        {
            await DisplayAlert("Error", "Por favor rellena todos los campos", "OK");
            return;
        }

        // Aquí irá la lógica real de unirse al grupo
        await Navigation.PushAsync(new GroupsChat());
    }
}