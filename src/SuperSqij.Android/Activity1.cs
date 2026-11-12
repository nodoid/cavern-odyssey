using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace SuperSqij.Android
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@mipmap/ic_launcher",
        Theme = "@style/SuperSqijTheme",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.UiMode
    )]
    public class Activity1 : AndroidGameActivity
    {
        private SuperSqijGame? _game;
        private View? _view;

        protected override void OnCreate(Bundle? bundle)
        {
            base.OnCreate(bundle);

            _game = new SuperSqijGame();
            _view = _game.Services.GetService(typeof(View)) as View;

            if (_view != null)
            {
                _view.KeepScreenOn = true;
                SetContentView(_view);
                HideSystemBars();
            }
            _game.Run();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus) HideSystemBars();
        }

        private void HideSystemBars()
        {
            if (Window == null) return;
#pragma warning disable CA1416
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window.SetDecorFitsSystemWindows(false);
                var controller = Window.InsetsController;
                controller?.Hide(WindowInsets.Type.SystemBars());
            }
            else
            {
                Window.AddFlags(WindowManagerFlags.Fullscreen);
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)
                    (SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky);
            }
#pragma warning restore CA1416
        }
    }
}
