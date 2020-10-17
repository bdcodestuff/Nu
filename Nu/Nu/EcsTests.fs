﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu.Tests
open System
open Xunit
open Prime
open Nu
module EcsTests =

    type [<NoEquality; NoComparison; Struct>] Skin =
        { mutable RefCount : int
          mutable Color : Color }
        interface Skin Component with
            member this.RefCount with get () = this.RefCount and set value = this.RefCount <- value
            member this.AllocateJunctions _ = [||]
            member this.ResizeJunctions _ _ _ = ()
            member this.MoveJunction _ _ _ _ = ()
            member this.Junction _ _ _ = this
            member this.Disjunction _ _ _ = ()

    type [<NoEquality; NoComparison; Struct>] Airship =
        { mutable RefCount : int
          Transform : Transform ComponentRef
          Skin : Skin ComponentRef }
        interface Airship Component with
            member this.RefCount with get () = this.RefCount and set value = this.RefCount <- value
            member this.AllocateJunctions ecs = [|ecs.AllocateArray<Transform> "Transform"; ecs.AllocateArray<Skin> "Skin"|]
            member this.ResizeJunctions size junctions ecs = ecs.ResizeJunction<Transform> size junctions.[0]; ecs.ResizeJunction<Skin> size junctions.[1]
            member this.MoveJunction src dst junctions ecs = ecs.MoveJunction<Transform> src dst junctions.[0]; ecs.MoveJunction<Skin> src dst junctions.[1]
            member this.Junction index junctions ecs = { id this with Transform = ecs.Junction<Transform> index junctions.[0]; Skin = ecs.Junction<Skin> index junctions.[1] }
            member this.Disjunction index junctions ecs = ecs.Disjunction<Transform> index junctions.[0]; ecs.Disjunction<Skin> index junctions.[1]

    type [<NoEquality; NoComparison; Struct>] Node =
        { mutable RefCount : int
          Transform : Transform }
        interface Node Component with
            member this.RefCount with get () = this.RefCount and set value = this.RefCount <- value
            member this.AllocateJunctions _ = [||]
            member this.ResizeJunctions _ _ _ = ()
            member this.MoveJunction _ _ _ _ = ()
            member this.Junction _ _ _ = this
            member this.Disjunction _ _ _ = ()

    type [<NoEquality; NoComparison; Struct>] Prop =
        { mutable RefCount : int
          Transform : Node ComponentRef
          NodeId : Guid }
        interface Prop Component with
            member this.RefCount with get () = this.RefCount and set value = this.RefCount <- value
            member this.AllocateJunctions ecs = [|ecs.AllocateArray<Node> "Node"|]
            member this.ResizeJunctions size junctions ecs = ecs.ResizeJunction<Node> size junctions.[0]
            member this.MoveJunction src dst junctions ecs = ecs.MoveJunction<Node> src dst junctions.[0]
            member this.Junction index junctions ecs = { id this with Transform = ecs.Junction<Node> index junctions.[0] }
            member this.Disjunction index junctions ecs = ecs.Disjunction<Node> index junctions.[0]

    let example (world : World) =

        // create our ecs
        let ecs = Ecs<World> ()

        // create and register our transform system
        let _ = ecs.RegisterSystem (SystemCorrelated<Transform, World> ecs)

        // create and register our skin system
        let _ = ecs.RegisterSystem (SystemCorrelated<Skin, World> ecs)

        // create and register our airship system
        let airshipSystem = ecs.RegisterSystem (SystemCorrelated<Airship, World> ecs)

        // define our airship system's update behavior
        let _ = ecs.Subscribe EcsEvents.Update (fun _ _ _ world ->
            let comps = airshipSystem.Components.Array
            for i in 0 .. comps.Length - 1 do
                let comp = &comps.[i]
                if  comp.RefCount > 0 then
                    comp.Transform.Index.Enabled <- i % 2 = 0
                    comp.Skin.Index.Color.A <- byte 128
            world)

        // create and register our airship
        let airshipId = ecs.RegisterCorrelated Unchecked.defaultof<Airship> airshipSystem.Name Gen.id

        // change some airship properties
        let airship = ecs.IndexCorrelated<Airship> airshipSystem.Name airshipId
        airship.Index.Transform.Index.Position.X <- 0.5f
        airship.Index.Skin.Index.Color.R <- byte 16

        // for non-junctioned entities, you can alternatively construct and use a much slower entity reference
        let airshipRef = ecs.GetEntityRef airshipId
        airshipRef.Index<Transform>().Position.Y <- 5.0f
        airshipRef.Index<Skin>().Color.G <- byte 255

        // invoke update behavior
        ecs.Publish EcsEvents.Update () ecs.GlobalSystem world