﻿namespace MyGame
open Prime
open Nu
open Nu.Declarative

[<AutoOpen>]
module MyGameplay =

    // this is our Elm-style model type. Either we're playing or we're quitting back to the title screen.
    type Gameplay =
        | Playing
        | Quitting

    // this is our Elm-style message type.
    type GameplayMessage =
        | Quit

    // this is our Elm-style command type. Commands are used instead of messages when things like physics are involved.
    type GameplayCommand =
        | MoveLeft
        | MoveRight
        | Jump
        | EyeTrack
        | Nop

    // this extends the Screen API to expose the above model. This is an unfortunate bit of boilerplate
    // needed when defining a new dispatcher.
    type Screen with
        member this.GetGameplay = this.GetModelGeneric<Gameplay>
        member this.SetGameplay = this.SetModelGeneric<Gameplay>
        member this.Gameplay = this.ModelGeneric<Gameplay> ()

    // this is the screen dispatcher that defines the screen where gameplay takes place
    type MyGameplayDispatcher () =
        inherit ScreenDispatcher<Gameplay, GameplayMessage, GameplayCommand> (Quitting)

        // here we channel from events to signals
        override this.Channel (_, _) =
            [Simulants.Game.KeyboardKeyDownEvent =|> fun evt ->
                if evt.Data.KeyboardKey = KeyboardKey.Up && not evt.Data.Repeated then cmd Jump
                else cmd Nop
             Simulants.Gameplay.Screen.UpdateEvent =|> fun _ ->
                if KeyboardState.isKeyDown KeyboardKey.Left then cmd MoveLeft
                elif KeyboardState.isKeyDown KeyboardKey.Right then cmd MoveRight
                else cmd Nop
             Simulants.Gameplay.Screen.PostUpdateEvent => cmd EyeTrack]

        // here we handle the above messages
        override this.Message (_, message, _, _) =
            match message with
            | Quit -> just Quitting

        // here we handle the above commands
        override this.Command (_, command, _, world) =
            let world =
                match command with
                | MoveLeft ->
                    let physicsId = Simulants.Gameplay.Scene.Player.GetPhysicsId world
                    if World.isBodyOnGround physicsId world
                    then World.applyBodyForce (v2 -2000.0f 0.0f) physicsId world
                    else World.applyBodyForce (v2 -500.0f 0.0f) physicsId world
                | MoveRight ->
                    let physicsId = Simulants.Gameplay.Scene.Player.GetPhysicsId world
                    if World.isBodyOnGround physicsId world
                    then World.applyBodyForce (v2 2000.0f 0.0f) physicsId world
                    else World.applyBodyForce (v2 500.0f 0.0f) physicsId world
                | Jump ->
                    let physicsId = Simulants.Gameplay.Scene.Player.GetPhysicsId world
                    if World.isBodyOnGround physicsId world then
                        let world = World.applyBodyForce (v2 0.0f 90000.0f) physicsId world
                        World.playSound Constants.Audio.SoundVolumeDefault (asset "Gameplay" "Jump") world
                    else world
                | EyeTrack ->
                    if World.getUpdateRate world <> 0L
                    then Simulants.Game.SetEyeCenter (Simulants.Gameplay.Scene.Player.GetCenter world) world
                    else world
                | Nop -> world
            just world

        // here we describe the content of the game including the level, the hud, and the player
        override this.Content (_, screen) =

            [// the gui group
             Content.group Simulants.Gameplay.Gui.Group.Name []
                 [Content.button Simulants.Gameplay.Gui.Quit.Name
                     [Entity.Text == "Quit"
                      Entity.Position == v2 260.0f -260.0f
                      Entity.Elevation == 10.0f
                      Entity.ClickEvent ==> msg Quit]]

             // the player group
             Content.groupIfScreenSelected screen $ fun _ _ ->
                Content.group Simulants.Gameplay.Scene.Group.Name []
                    [Content.character Simulants.Gameplay.Scene.Player.Name
                        [Entity.Position == v2 0.0f 0.0f
                         Entity.Size == v2 108.0f 108.0f]]

             // the level group
             Content.groupIfScreenSelected screen $ fun _ _ ->
                Content.groupFromFile Simulants.Gameplay.Level.Group.Name "Assets/Gameplay/Level.nugroup"]