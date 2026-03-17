namespace MauiFront
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void BotonEntrar(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(userEntry.Text) && !string.IsNullOrWhiteSpace(userEntry.Text)  )
            {
                await Shell.Current.GoToAsync("groups");
            }
            else
            {
                DisplayAlert("Error","Pon usuario y contraseña!","Ok");
            }
        }

        private async void CrearNuevoGrupo(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CrearGrupo");
        }



    }
}
