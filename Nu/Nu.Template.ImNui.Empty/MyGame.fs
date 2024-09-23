﻿namespace MyGame
open System
open System.Numerics
open Prime
open Nu

// this is our top-level ImNui model type. It determines what state the game is in. To learn about ImNui in Nu, see -
// https://github.com/bryanedds/Nu/wiki/Immediate-Mode-for-Games-via-ImNui
type MyGame =
    { MyGameTime : int64 }
    static member initial = { MyGameTime = 0L }

// this extends the Game API to expose the above ImNui model as a property.
[<AutoOpen>]
module MyGameExtensions =
    type Game with
        member this.GetMyGame world = this.GetModelGeneric<MyGame> world
        member this.SetMyGame value world = this.SetModelGeneric<MyGame> value world
        member this.MyGame = this.ModelGeneric<MyGame> ()

// this is the dispatcher that customizes the top-level behavior of our game.
type MyGameDispatcher () =
    inherit GameDispatcher<MyGame> (MyGame.initial)

    // here we handle running the game
    override this.Run (myGame, _, world) =

        // run in game context
        let (_, world) = World.beginScreen "Screen" true Vanilla [] world
        let world = World.beginGroup "Group" [] world
        let world =
            World.doStaticModel "StaticModel"
                [Entity.Position .= v3 0.0f 0.0f -2.0f
                 Entity.Rotation @= Quaternion.CreateFromAxisAngle ((v3 1.0f 0.75f 0.5f).Normalized, myGame.MyGameTime % 360L |> single |> Math.DegreesToRadians)] world
        let world =
            match World.doButton "Exit" [Entity.Text .= "Exit"; Entity.Position .= v3 232.0f -144.0f 0.0f] world with
            | (true, world) -> World.exit world
            | (_, world) -> world
        let world = World.endGroup world
        let world = World.endScreen world

        // handle Alt+F4
        let world =
            if world.Unaccompanied && World.isKeyboardAltDown world && World.isKeyboardKeyDown KeyboardKey.F4 world
            then World.exit world
            else world

        // advance game time
        let gameDelta = world.GameDelta
        let myGame = { myGame with MyGameTime = myGame.MyGameTime + gameDelta.Updates }
        (myGame, world)