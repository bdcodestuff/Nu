﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2012-2016.

namespace Nu
open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open OpenTK
open Prime
open Nu

[<AutoOpen>]
module WorldScreenModule =

    type Screen with

        member this.GetId world = ScreenState.getId (World.getScreenState this world)
        member this.GetName world = ScreenState.getName (World.getScreenState this world)
        member this.GetXtension world = ScreenState.getXtension (World.getScreenState this world)
        member this.GetDispatcherNp world = ScreenState.getDispatcherNp (World.getScreenState this world)
        member this.GetCreationTimeStampNp world = ScreenState.getCreationTimeStampNp (World.getScreenState this world)
        member this.GetEntityTree world = ScreenState.getEntityTreeNp (World.getScreenState this world)
        member this.GetOptSpecialization world = ScreenState.getOptSpecialization (World.getScreenState this world)
        member this.GetTransitionStateNp world = ScreenState.getTransitionStateNp (World.getScreenState this world)
        member this.SetTransitionStateNp value world = World.updateScreenState (ScreenState.setTransitionStateNp value) this world
        member this.GetTransitionTicksNp world = ScreenState.getTransitionTicksNp (World.getScreenState this world)
        member this.SetTransitionTicksNp value world = World.updateScreenState (ScreenState.setTransitionTicksNp value) this world
        member this.GetIncoming world = ScreenState.getIncoming (World.getScreenState this world)
        member this.SetIncoming value world = World.updateScreenState (ScreenState.setIncoming value) this world
        member this.GetOutgoing world = ScreenState.getOutgoing (World.getScreenState this world)
        member this.SetOutgoing value world = World.updateScreenState (ScreenState.setOutgoing value) this world
        member this.GetPersistent world = ScreenState.getPersistent (World.getScreenState this world)
        member this.SetPersistent value world = World.updateScreenState (ScreenState.setPersistent value) this world

        /// The dynamic look-up operator.
        member this.Get propertyName world : 'r =
            ScreenState.(?) (World.getScreenState this world, propertyName)

        /// The dynamic assignment operator.
        member this.Set propertyName (value : 'a) world = 
            World.setScreenState (ScreenState.(?<-) (World.getScreenState this world, propertyName, value)) this world

        /// Query that a screen is in an idling state (not transitioning in nor out).
        member this.IsIdling world =
            this.GetTransitionStateNp world = IdlingState

        /// Query that a screen dispatches in the same manner as the dispatcher with the target type.
        member this.DispatchesAs (dispatcherTargetType : Type) world =
            Reflection.dispatchesAs dispatcherTargetType (this.GetDispatcherNp world)

    type World with

        static member private registerScreen (screen : Screen) world =
            let dispatcher = screen.GetDispatcherNp world : ScreenDispatcher
            dispatcher.Register (screen, world)

        static member private unregisterScreen (screen : Screen) world =
            let dispatcher = screen.GetDispatcherNp world : ScreenDispatcher
            dispatcher.Unregister (screen, world)

        static member internal updateScreen (screen : Screen) world =
            let dispatcher = screen.GetDispatcherNp world
            let world = dispatcher.Update (screen, world)
            let eventTrace = EventTrace.record "World" "updateScreen" EventTrace.empty
            World.publish7 World.getSubscriptionsSorted World.sortSubscriptionsByHierarchy () (Events.Update ->- screen) eventTrace Simulants.Game world

        static member internal actualizeScreen (screen : Screen) world =
            let dispatcher = screen.GetDispatcherNp world
            dispatcher.Actualize (screen, world)

        static member internal addScreen mayReplace screenState screen world =
            let isNew = not ^ World.containsScreen screen world
            if isNew || mayReplace then
                let world = World.addScreenState screenState screen world
                if isNew then
                    let world = World.registerScreen screen world
                    let eventTrace = EventTrace.record "World" "addScreen" EventTrace.empty
                    World.publish () (Events.ScreenAdd ->- screen) eventTrace screen world
                else world
            else failwith ^ "Adding a screen that the world already contains at address '" + scstring screen.ScreenAddress + "'."

        /// Remove a screen from the world. Can be dangerous if existing in-flight publishing depends on the screen's
        /// existence. Use with caution.
        static member removeScreen screen world =
            let eventTrace = EventTrace.record "World" "removeScreen" EventTrace.empty
            let world = World.publish () (Events.ScreenRemoving ->- screen) eventTrace screen world
            if World.containsScreen screen world then
                let world = World.unregisterScreen screen world
                let groups = World.proxyGroups screen world
                let world = World.destroyGroupsImmediate groups world
                World.removeScreenState screen world
            else world

        /// Query that the world contains a screen.
        static member containsScreen screen world =
            Option.isSome ^ World.getOptScreenState screen world

        /// Get all the world's screens.
        static member proxyScreens world =
            Vmap.fold
                (fun state _ (screenAddress, _) -> Screen.proxy screenAddress :: state)
                []
                (World.getScreenDirectory world) :>
                _ seq

        /// Destroy a screen in the world immediately. Can be dangerous if existing in-flight publishing depends on the
        /// screen's existence. Use with caution.
        static member destroyScreenImmediate screen world =
            World.removeScreen screen world

        /// Destroy a screen in the world on the next tick. Use this rather than destroyScreenImmediate unless you need
        /// the latter's specific behavior.
        static member destroyScreen screen world =
            let tasklet =
                { ScheduledTime = World.getTickTime world
                  Operation = fun world -> World.destroyScreenImmediate screen world }
            World.addTasklet tasklet world

        /// Create a screen and add it to the world.
        static member createScreen dispatcherName optSpecialization optName world =
            let dispatchers = World.getScreenDispatchers world
            let dispatcher = Map.find dispatcherName dispatchers
            let screenState = ScreenState.make optSpecialization optName dispatcher
            Reflection.attachProperties dispatcher screenState
            let screen = ntos ^ ScreenState.getName screenState
            let world = World.addScreen false screenState screen world
            (screen, world)
        
        /// Create a screen with a dissolving transition, and add it to the world.
        static member createDissolveScreen dissolveData dispatcherName optSpecialization optName world =
            let optDissolveImage = Some dissolveData.DissolveImage
            let (screen, world) = World.createScreen dispatcherName optSpecialization optName world
            let world = screen.SetIncoming { Transition.make Incoming with TransitionLifetime = dissolveData.IncomingTime; OptDissolveImage = optDissolveImage } world
            let world = screen.SetOutgoing { Transition.make Outgoing with TransitionLifetime = dissolveData.OutgoingTime; OptDissolveImage = optDissolveImage } world
            (screen, world)

        /// Write a screen to a screen descriptor.
        static member writeScreen (screen : Screen) screenDescriptor world =
            let screenState = World.getScreenState screen world
            let screenDispatcherName = getTypeName ^ ScreenState.getDispatcherNp screenState
            let screenDescriptor = { screenDescriptor with ScreenDispatcher = screenDispatcherName }
            let getScreenProperties = Reflection.writeMemberValuesFromTarget tautology3 screenDescriptor.ScreenProperties screenState
            let screenDescriptor = { screenDescriptor with ScreenProperties = getScreenProperties }
            let groups = World.proxyGroups screen world
            World.writeGroups groups screenDescriptor world

        /// Write multiple screens to a game descriptor.
        static member writeScreens screens gameDescriptor world =
            screens |>
            Seq.sortBy (fun (screen : Screen) -> screen.GetCreationTimeStampNp world) |>
            Seq.filter (fun (screen : Screen) -> screen.GetPersistent world) |>
            Seq.fold (fun screenDescriptors screen -> World.writeScreen screen ScreenDescriptor.empty world :: screenDescriptors) gameDescriptor.Screens |>
            fun screenDescriptors -> { gameDescriptor with Screens = screenDescriptors }

        /// Write a screen to a file.
        static member writeScreenToFile (filePath : string) screen world =
            let filePathTmp = filePath + ".tmp"
            let screenDescriptor = World.writeScreen screen ScreenDescriptor.empty world
            let screenDescriptorStr = scstring screenDescriptor
            let screenDescriptorPretty = Symbol.prettyPrint String.Empty screenDescriptorStr
            File.WriteAllText (filePathTmp, screenDescriptorPretty)
            File.Delete filePath
            File.Move (filePathTmp, filePath)

        /// Read a screen from a screen descriptor.
        static member readScreen screenDescriptor optName world =
            
            // create the dispatcher
            let dispatcherName = screenDescriptor.ScreenDispatcher
            let dispatchers = World.getScreenDispatchers world
            let dispatcher =
                match Map.tryFind dispatcherName dispatchers with
                | Some dispatcher -> dispatcher
                | None ->
                    Log.info ^ "Could not locate dispatcher '" + dispatcherName + "'."
                    let dispatcherName = typeof<ScreenDispatcher>.Name
                    Map.find dispatcherName dispatchers
            
            // make the bare screen state with name as id
            let screenState = ScreenState.make None None dispatcher

            // attach the screen state's instrinsic properties from its dispatcher if any
            Reflection.attachProperties (ScreenState.getDispatcherNp screenState) screenState

            // read the screen state's value
            Reflection.readMemberValuesToTarget screenDescriptor.ScreenProperties screenState

            // apply the name if one is provided
            let screenState =
                match optName with
                | Some name -> ScreenState.setName name screenState
                | None -> screenState
            
            // add the screen's state to the world
            let screen = ntos ^ ScreenState.getName screenState
            let screenState =
                if World.containsScreen screen world
                then ScreenState.setEntityTreeNp (screen.GetEntityTree world) screenState
                else screenState
            let world = World.addScreen true screenState screen world
            
            // read the screen's groups
            let world = World.readGroups screenDescriptor screen world |> snd
            (screen, world)

        /// Read a screen from a file.
        static member readScreenFromFile (filePath : string) optName world =
            let screenDescriptorStr = File.ReadAllText filePath
            let screenDescriptor = scvalue<ScreenDescriptor> screenDescriptorStr
            World.readScreen screenDescriptor optName world

        /// Read multiple screens from a game descriptor.
        static member readScreens gameDescriptor world =
            Seq.foldBack
                (fun screenDescriptor (screens, world) ->
                    let (screen, world) = World.readScreen screenDescriptor None world
                    (screen :: screens, world))
                gameDescriptor.Screens
                ([], world)

namespace Debug
open Prime
open Nu
open System.Reflection
open System.Collections.Generic
type Screen =

    /// Provides a view of all the built-in properties of a screen. Useful for debugging such as with
    /// the Watch feature in Visual Studio.
    static member viewProperties screen world =
        let state = World.getScreenState screen world
        state |>
        getType |>
        getProperties |>
        Array.map (fun (property : PropertyInfo) -> (property.Name, property.GetValue state))
        
    /// Provides a view of all the xtension properties of a screen. Useful for debugging such as
    /// with the Watch feature in Visual Studio.
    static member viewXProperties screen world =
        let state = World.getScreenState screen world
        Xtension.toSeq (ScreenState.getXtension state) |>
        Array.ofSeq |>
        Array.sortBy fst |>
        Array.map (fun (name, property) -> (name, property.PropertyValue))

    /// Provides a full view of all the member values of a screen. Useful for debugging such
    /// as with the Watch feature in Visual Studio.
    static member view screen world =
        Array.append (Screen.viewProperties screen world) (Screen.viewXProperties screen world)