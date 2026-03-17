namespace MauiFront
{
    public partial class GroupsPage : ContentPage
    {
        public GroupsPage()
        {
            InitializeComponent();


        }


        private async void CrearNuevoGrupo(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CrearGrupo");
        }



    }

}
