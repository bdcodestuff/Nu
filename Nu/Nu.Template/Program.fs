﻿namespace MyGame
open System
open Nu
module Program =

    // this is a plugin for the Nu game engine that directs the execution of your application and editor
    type MyPlugin () =
        inherit NuPlugin ()

        // this exposes different editing modes in the editor
        override this.Modes =
            Map.ofSeq
                ["Title", fun world -> Simulants.Game.SetModel Title world
                 "Credits", fun world -> Simulants.Game.SetModel Credits world
                 "Gameplay", fun world -> Simulants.Game.SetModel (Gameplay Playing) world]

    // this the entry point for your Nu application
    let [<EntryPoint; STAThread>] main _ =

        // this specifies the window configuration used to display the game
        let sdlWindowConfig = { SdlWindowConfig.defaultConfig with WindowTitle = "MyGame" }
        
        // this specifies the configuration of the game engine's use of SDL
        let sdlConfig = { SdlConfig.defaultConfig with ViewConfig = NewWindow sdlWindowConfig }

        // use the default world config with the above SDL config
        let worldConfig = { WorldConfig.defaultConfig with SdlConfig = sdlConfig }

        // initialize Nu
        Nu.init worldConfig.NuConfig

        // run the engine with the given config and plugin
        World.run worldConfig (MyPlugin ())