using System;
using Foundation;
using UIKit;

namespace SuperSqij.iOS
{
    /// <summary>
    /// iOS 27 requires the UIScene lifecycle. MonoGame 3.8.6 still creates a legacy
    /// UIWindow, so we attach that window to this scene in SuperSqijGame.Initialize().
    /// </summary>
    [Register("SceneDelegate")]
    public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        public static SceneDelegate? Instance;

        public UIWindowScene? Scene { get; private set; }

        [Export("window")]
        public UIWindow? Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            Console.WriteLine("SQIJ: SceneDelegate.WillConnect scene=" + scene.GetType().Name);
            if (scene is not UIWindowScene windowScene) return;
            Scene = windowScene;
            Instance = this;
            // Do not create a placeholder window here: it would stay above MonoGame's
            // window and swallow every touch. MonoGame creates, shows and becomes key
            // for its own window below, and we attach that window to this scene.
            Console.WriteLine("SQIJ: starting game");
            Program.RunGame();
            Console.WriteLine("SQIJ: RunGame returned");
            // MonoGame subscribes to these notifications inside StartRunLoop. In a scene
            // app the app was already active before that, so nudge the platform awake.
            NSNotificationCenter.DefaultCenter.PostNotificationName(
                UIApplication.DidBecomeActiveNotification, UIApplication.SharedApplication);
        }

        [Export("sceneDidBecomeActive:")]
        public void DidBecomeActive(UIScene scene) { }

        [Export("sceneWillResignActive:")]
        public void WillResignActive(UIScene scene) { }
    }
}
