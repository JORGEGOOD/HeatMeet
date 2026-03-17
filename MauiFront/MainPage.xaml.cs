namespace MauiFront
{
    public partial class MainPage : ContentPage
    {
        bool animating = true;

        public MainPage()
        {
            InitializeComponent();
        }

        // Animación lanzada al aparecer
        protected override void OnAppearing()
        {
            base.OnAppearing();
            animating = true;
            AnimateGradients();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            animating = false;
        }

        // 🌈 Animación continua para fondo y botón
        private async void AnimateGradients()
        {
            while (animating)
            {
                await AnimateToColors(bgGrad1, bgGrad2, btnGrad1, btnGrad2,
                                      Color.FromArgb("#6A0DAD"), Color.FromArgb("#BA68C8"),
                                      Color.FromArgb("#9C27B0"), Color.FromArgb("#6A0DAD"));
                await AnimateToColors(bgGrad1, bgGrad2, btnGrad1, btnGrad2,
                                      Color.FromArgb("#9C27B0"), Color.FromArgb("#6A0DAD"),
                                      Color.FromArgb("#6A0DAD"), Color.FromArgb("#BA68C8"));
            }
        }

        private Task AnimateToColors(GradientStop bg1, GradientStop bg2,
                                     GradientStop btn1, GradientStop btn2,
                                     Color bgTarget1, Color bgTarget2,
                                     Color btnTarget1, Color btnTarget2)
        {
            var tcs = new TaskCompletionSource<bool>();
            var startBg1 = bg1.Color;
            var startBg2 = bg2.Color;
            var startBtn1 = btn1.Color;
            var startBtn2 = btn2.Color;

            var animation = new Animation(v =>
            {
                bg1.Color = LerpColor(startBg1, bgTarget1, v);
                bg2.Color = LerpColor(startBg2, bgTarget2, v);
                btn1.Color = LerpColor(startBtn1, btnTarget1, v);
                btn2.Color = LerpColor(startBtn2, btnTarget2, v);
            });

            animation.Commit(this, "GradientAnimation", 16, 2500, Easing.Linear,
                             (v, c) => tcs.SetResult(true));
            return tcs.Task;
        }

        private Color LerpColor(Color from, Color to, double t)
        {
            return Color.FromRgba(
                from.Red + (to.Red - from.Red) * t,
                from.Green + (to.Green - from.Green) * t,
                from.Blue + (to.Blue - from.Blue) * t,
                from.Alpha + (to.Alpha - from.Alpha) * t
            );
        }

        // Botón con animación, loader y validación
        private async void BotonEntrar(object sender, EventArgs e)
        {
            await loginButton.ScaleTo(0.95, 100, Easing.CubicOut);
            await loginButton.ScaleTo(1, 100, Easing.CubicIn);

            if (!string.IsNullOrWhiteSpace(userEntry.Text) &&
                !string.IsNullOrWhiteSpace(passwordEntry.Text))
            {
                loginButton.Text = "";
                loader.IsVisible = true;
                loader.IsRunning = true;
                loginButton.IsEnabled = false;

                await Task.Delay(1500);

                await Shell.Current.GoToAsync("groups");

                loader.IsRunning = false;
                loader.IsVisible = false;
                loginButton.Text = "Entrar";
                loginButton.IsEnabled = true;
            }
            else
            {
                // Shake efecto error
                await loginButton.TranslateTo(-10, 0, 50);
                await loginButton.TranslateTo(10, 0, 50);
                await loginButton.TranslateTo(-5, 0, 50);
                await loginButton.TranslateTo(5, 0, 50);
                await loginButton.TranslateTo(0, 0, 50);

                await DisplayAlert("Error", "Pon usuario y contraseña!", "Ok");
            }
        }
    }
}