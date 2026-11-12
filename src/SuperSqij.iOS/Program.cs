using System;
using Foundation;
using UIKit;

namespace SuperSqij.iOS
{
    [Register("AppDelegate")]
    class Program : UIApplicationDelegate
    {
        private static SuperSqijGame? _game;
        internal static bool Autostart;

        internal static void RunGame()
        {
            if (_game != null) return;
            Console.WriteLine("SQIJ: creating game");
            try
            {
                _game = new SuperSqijGame();
                _game.Run();
                Console.WriteLine("SQIJ: game run returned");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQIJ: FATAL " + ex);
                throw;
            }
        }

        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }

        public override bool FinishedLaunching(UIApplication app, NSDictionary launchOptions)
        {
            Console.WriteLine("SQIJ: FinishedLaunching sceneDelegateClass=" +
                              (ObjCRuntime.Class.GetHandle("SceneDelegate") != IntPtr.Zero));
#if DEBUG
            // Development shortcut only: release builds must not carry undocumented flags.
            foreach (var arg in NSProcessInfo.ProcessInfo.Arguments)
                if (arg is "-autostart" or "--autostart") Autostart = true;
#endif
            return true;
        }

        public override UISceneConfiguration GetConfiguration(UIApplication application,
            UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        {
            Console.WriteLine("SQIJ: GetConfiguration");
            var config = new UISceneConfiguration("Cavern Odyssey", connectingSceneSession.Role);
            config.DelegateType = typeof(SceneDelegate);
            return config;
        }
    }
}
