namespace MauiFront
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            //registrar "LoginPage" como "login"
            Routing.RegisterRoute("groups", typeof(GroupsPage));
        }
    }
}
