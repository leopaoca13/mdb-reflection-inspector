using System;
using GameSDK.ModHost;

namespace MyMod
{
    /// <summary>
    /// Main mod entry point.
    /// </summary>
    [Mod("MyAuthor.MyMod", "MyMod", "1.0.0", Author = "MyAuthor", Description = "An MDB Framework mod")]
    public class Mod : ModBase
    {
        /// <summary>Called once when the mod is loaded.</summary>
        public override void OnLoad()
        {
            Logger.Info("MyMod loaded!");
            ImGuiWindow.Register(Logger);
        }

        /// <summary>Called every frame.</summary>
        public override void OnUpdate()
        {
            // Your per-frame logic here
        }
    }
}
