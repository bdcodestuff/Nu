﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.Collections.Generic
open Prime
open Nu

[<AutoOpen; ModuleBinding>]
module WorldModuleGroup =

    /// Dynamic property getters / setters.
    let internal GroupGetters = Dictionary<string, Group -> World -> Property> StringComparer.Ordinal
    let internal GroupSetters = Dictionary<string, Property -> Group -> World -> struct (bool * World)> StringComparer.Ordinal

    type World with
    
        static member private groupStateFinder (group : Group) world =
            UMap.tryFind group world.GroupStates

        static member private groupStateAdder groupState (group : Group) world =
            let screen = group.Screen
            let simulants =
                match world.Simulants.TryGetValue (screen :> Simulant) with
                | (true, groupsOpt) ->
                    match groupsOpt with
                    | Some groups ->
                        let groups = USet.add (group :> Simulant) groups
                        UMap.add (screen :> Simulant) (Some groups) world.Simulants
                    | None ->
                        let groups = USet.singleton HashIdentity.Structural (World.getCollectionConfig world) (group :> Simulant)
                        UMap.add (screen :> Simulant) (Some groups) world.Simulants
                | (false, _) -> failwith ("Cannot add group '" + scstring group + "' to non-existent screen.")
            let simulants =
                if not (UMap.containsKey (group :> Simulant) simulants)
                then UMap.add (group :> Simulant) None simulants
                else simulants
            let groupStates = UMap.add group groupState world.GroupStates
            World.choose { world with Simulants = simulants; GroupStates = groupStates }
        
        static member private groupStateRemover (group : Group) world =
            let screen = group.Screen
            let simulants =
                match world.Simulants.TryGetValue (screen :> Simulant) with
                | (true, groupsOpt) ->
                    match groupsOpt with
                    | Some groups ->
                        let groups = USet.remove (group :> Simulant) groups
                        if USet.isEmpty groups
                        then UMap.add (screen :> Simulant) None world.Simulants
                        else UMap.add (screen :> Simulant) (Some groups) world.Simulants
                    | None -> world.Simulants
                | (false, _) -> world.Simulants
            let simulants = UMap.remove (group :> Simulant) simulants
            let groupStates = UMap.remove group world.GroupStates
            World.choose { world with Simulants = simulants; GroupStates = groupStates }

        static member private groupStateSetter groupState (group : Group) world =
#if DEBUG
            if not (UMap.containsKey group world.GroupStates) then
                failwith ("Cannot set the state of a non-existent group '" + scstring group + "'")
#endif
            let groupStates = UMap.add group groupState world.GroupStates
            World.choose { world with GroupStates = groupStates }

        static member private addGroupState groupState group world =
            World.groupStateAdder groupState group world

        static member internal removeGroupState group world =
            World.groupStateRemover group world

        static member internal publishGroupChange propertyName (propertyPrevious : obj) (propertyValue : obj) (group : Group) world =

            // publish change binding
            let world =
                World.publishChangeBinding propertyName group world

            // publish event binding
            let world =
                let changeData = { Name = propertyName; Previous = propertyPrevious; Value = propertyValue }
                let groupNames = Address.getNames group.GroupAddress
                let changeEventAddress = rtoa<ChangeData> [|"Change"; propertyName; "Event"; groupNames.[0]; groupNames.[1]|]
                let eventTrace = EventTrace.debug "World" "publishGroupChange" "" EventTrace.empty
                World.publishPlus changeData changeEventAddress eventTrace group false false world

            // fin
            world

        static member private getGroupStateOpt group world =
            World.groupStateFinder group world

        static member internal getGroupState group world =
            match World.getGroupStateOpt group world with
            | Some groupState -> groupState
            | None -> failwith ("Could not find group '" + scstring group + "'.")

        static member internal setGroupState groupState group world =
            World.groupStateSetter groupState group world

        static member internal getGroupXtensionProperties group world =
            let groupState = World.getGroupState group world
            groupState.Xtension |> Xtension.toSeq |> Seq.toList

        /// Check that a group exists in the world.
        static member internal getGroupExists group world =
            Option.isSome (World.getGroupStateOpt group world)

        static member internal getGroupModelProperty group world = (World.getGroupState group world).Model
        static member internal getGroupForge group world = (World.getGroupState group world).Forge
        static member internal getGroupDispatcher group world = (World.getGroupState group world).Dispatcher
        static member internal getGroupVisible group world = (World.getGroupState group world).Visible
        static member internal getGroupPersistent group world = (World.getGroupState group world).Persistent
        static member internal getGroupDestroying (group : Group) world = List.exists ((=) (group :> Simulant)) (World.getDestructionListRev world)
        static member internal getGroupOrder group world = (World.getGroupState group world).Order
        static member internal getGroupScriptFrame group world = (World.getGroupState group world).ScriptFrame
        static member internal getGroupId group world = (World.getGroupState group world).Id
        static member internal getGroupName group world = (World.getGroupState group world).Name

        static member internal setGroupModelProperty (value : DesignerProperty) group world =
            let groupState = World.getGroupState group world
            let previous = groupState.Model
            if value.DesignerValue =/= previous.DesignerValue then
                let struct (groupState, world) =
                    let groupState = { groupState with Model = { DesignerType = value.DesignerType; DesignerValue = value.DesignerValue }}
                    struct (groupState, World.setGroupState groupState group world)
                let world = World.publishGroupChange (nameof groupState.Model) previous.DesignerValue value.DesignerValue group world
                let world = (World.getGroupDispatcher group world).TryReforge (group, world)
                struct (true, world)
            else struct (false, world)

        static member internal getGroupModel<'a> group world =
            match (World.getGroupState group world).Model.DesignerValue with
            | :? 'a as model -> model
            | null -> null :> obj :?> 'a
            | modelObj -> modelObj |> valueToSymbol |> symbolToValue

        static member internal setGroupModel<'a> (value : 'a) group world =
            let groupState = World.getGroupState group world
            let valueObj = value :> obj
            let previous = groupState.Model
            if valueObj =/= previous.DesignerValue then
                let struct (groupState, world) =
                    let groupState = { groupState with Model = { DesignerType = typeof<'a>; DesignerValue = valueObj }}
                    struct (groupState, World.setGroupState groupState group world)
                let world = World.publishGroupChange (nameof groupState.Model) previous.DesignerValue value group world
                let world = (World.getGroupDispatcher group world).TryReforge (group, world)
                struct (true, world)
            else struct (false, world)

        static member internal setGroupForge (value : GroupForge) group world =
            let screenState = World.getGroupState group world
            let screenState = { screenState with Forge = value }
            World.setGroupState screenState group world

        static member internal setGroupVisible value group world =
            let groupState = World.getGroupState group world
            let previous = groupState.Visible
            if value <> previous
            then struct (true, world |> World.setGroupState { groupState with Visible = value } group |> World.publishGroupChange (nameof groupState.Visible) previous value group)
            else struct (false, world)

        static member internal setGroupPersistent value group world =
            let groupState = World.getGroupState group world
            let previous = groupState.Persistent
            if value <> previous
            then struct (true, world |> World.setGroupState { groupState with Persistent = value } group |> World.publishGroupChange (nameof groupState.Persistent) previous value group)
            else struct (false, world)

        static member internal setGroupScriptFrame value group world =
            let groupState = World.getGroupState group world
            let previous = groupState.ScriptFrame
            if value <> previous
            then struct (true, world |> World.setGroupState { groupState with ScriptFrame = value } group |> World.publishGroupChange (nameof groupState.ScriptFrame) previous value group)
            else struct (false, world)

        static member internal tryGetGroupXtensionProperty (propertyName, group, world, property : _ outref) =
            if World.getGroupExists group world
            then GroupState.tryGetProperty (propertyName, World.getGroupState group world, &property)
            else false

        static member internal getGroupXtensionProperty propertyName group world =
            let mutable property = Unchecked.defaultof<_>
            match GroupState.tryGetProperty (propertyName, World.getGroupState group world, &property) with
            | true -> property
            | false -> failwithf "Could not find property '%s'." propertyName

        static member internal getGroupXtensionValue<'a> propertyName group world =
            let groupState = World.getGroupState group world
            let property = GroupState.getProperty propertyName groupState
            property.PropertyValue :?> 'a

        static member internal tryGetGroupProperty (propertyName, group, world, property : _ outref) =
            match GroupGetters.TryGetValue propertyName with
            | (true, getter) ->
                if World.getGroupExists group world then
                    property <- getter group world
                    true
                else false
            | (false, _) ->
                World.tryGetGroupXtensionProperty (propertyName, group, world, &property)

        static member internal getGroupProperty propertyName group world =
            match GroupGetters.TryGetValue propertyName with
            | (true, getter) -> getter group world
            | (false, _) -> World.getGroupXtensionProperty propertyName group world

        static member internal trySetGroupXtensionPropertyFast propertyName property group world =
            let groupState = World.getGroupState group world
            match GroupState.tryGetProperty (propertyName, groupState) with
            | (true, propertyOld) ->
                if property.PropertyValue =/= propertyOld.PropertyValue then
                    let struct (success, groupState) = GroupState.trySetProperty propertyName property groupState
                    let world = World.setGroupState groupState group world
                    if success then World.publishGroupChange propertyName propertyOld.PropertyValue property.PropertyValue group world else world
                else world
            | (false, _) -> world

        static member internal trySetGroupXtensionProperty propertyName property group world =
            let groupState = World.getGroupState group world
            match GroupState.tryGetProperty (propertyName, groupState) with
            | (true, propertyOld) ->
                if property.PropertyValue =/= propertyOld.PropertyValue then
                    let struct (success, groupState) = GroupState.trySetProperty propertyName property groupState
                    let world = World.setGroupState groupState group world
                    if success
                    then struct (success, true, World.publishGroupChange propertyName propertyOld.PropertyValue property.PropertyValue group world)
                    else struct (false, true, world)
                else struct (false, false, world)
            | (false, _) -> struct (false, false, world)

        static member internal setGroupXtensionProperty propertyName property group world =
            let groupState = World.getGroupState group world
            let propertyOld = GroupState.getProperty propertyName groupState
            if property.PropertyValue =/= propertyOld.PropertyValue then
                let groupState = GroupState.setProperty propertyName property groupState
                let world = World.setGroupState groupState group world
                struct (true, World.publishGroupChange propertyName propertyOld.PropertyValue property.PropertyValue group world)
            else struct (false, world)

        static member internal trySetGroupPropertyFast propertyName property group world =
            match GroupSetters.TryGetValue propertyName with
            | (true, setter) ->
                if World.getGroupExists group world
                then setter property group world |> snd'
                else world
            | (false, _) ->
                World.trySetGroupXtensionPropertyFast propertyName property group world

        static member internal trySetGroupProperty propertyName property group world =
            match GroupSetters.TryGetValue propertyName with
            | (true, setter) ->
                if World.getGroupExists group world then
                    let struct (changed, world) = setter property group world
                    struct (true, changed, world)
                else (false, false, world)
            | (false, _) ->
                World.trySetGroupXtensionProperty propertyName property group world

        static member internal setGroupProperty propertyName property group world =
            match GroupSetters.TryGetValue propertyName with
            | (true, setter) ->
                if World.getGroupExists group world
                then setter property group world
                else struct (false, world)
            | (false, _) ->
                World.setGroupXtensionProperty propertyName property group world

        static member internal attachGroupProperty propertyName property group world =
            if World.getGroupExists group world then
                let groupState = World.getGroupState group world
                let groupState = GroupState.attachProperty propertyName property groupState
                let world = World.setGroupState groupState group world
                World.publishGroupChange propertyName property.PropertyValue property.PropertyValue group world
            else failwith ("Cannot attach group property '" + propertyName + "'; group '" + scstring group + "' is not found.")

        static member internal detachGroupProperty propertyName group world =
            if World.getGroupExists group world then
                let groupState = World.getGroupState group world
                let groupState = GroupState.detachProperty propertyName groupState
                World.setGroupState groupState group world
            else failwith ("Cannot detach group property '" + propertyName + "'; group '" + scstring group + "' is not found.")

        static member internal registerGroup group world =
            let dispatcher = World.getGroupDispatcher group world
            let world = dispatcher.Register (group, world)
            let eventTrace = EventTrace.debug "World" "registerGroup" "" EventTrace.empty
            let world = World.publishPlus () (Events.Register --> group) eventTrace group true false world
            let eventTrace = EventTrace.debug "World" "registerGroup" "LifeCycle" EventTrace.empty
            World.publishPlus (RegisterData group) (Events.LifeCycle (nameof Group)) eventTrace group true false world

        static member internal unregisterGroup group world =
            let dispatcher = World.getGroupDispatcher group world
            let eventTrace = EventTrace.debug "World" "unregisterGroup" "LifeCycle" EventTrace.empty
            let world = World.publishPlus (UnregisteringData group) (Events.LifeCycle (nameof Group)) eventTrace group true false world
            let eventTrace = EventTrace.debug "World" "unregisteringGroup" "" EventTrace.empty
            let world = World.publishPlus () (Events.Unregistering --> group) eventTrace group true false world
            dispatcher.Unregister (group, world)

        static member internal addGroup mayReplace groupState group world =
            let isNew = not (World.getGroupExists group world)
            if isNew || mayReplace then
                let world = World.addGroupState groupState group world
                if isNew then World.registerGroup group world else world
            else failwith ("Adding a group that the world already contains '" + scstring group + "'.")

        static member internal removeGroup3 removeEntities group world =
            if World.getGroupExists group world then
                let world = World.unregisterGroup group world
                let world = removeEntities group world
                World.removeGroupState group world
            else world

        /// View all of the properties of a group.
        static member viewGroupProperties group world =
            let state = World.getGroupState group world
            World.viewProperties state

    /// Initialize property getters.
    let private initGetters () =
        GroupGetters.Add ("Dispatcher", fun group world -> { PropertyType = typeof<GroupDispatcher>; PropertyValue = World.getGroupDispatcher group world })
        GroupGetters.Add ("Model", fun group world -> let designerProperty = World.getGroupModelProperty group world in { PropertyType = designerProperty.DesignerType; PropertyValue = designerProperty.DesignerValue })
        GroupGetters.Add ("Visible", fun group world -> { PropertyType = typeof<bool>; PropertyValue = World.getGroupVisible group world })
        GroupGetters.Add ("Persistent", fun group world -> { PropertyType = typeof<bool>; PropertyValue = World.getGroupPersistent group world })
        GroupGetters.Add ("Destroying", fun group world -> { PropertyType = typeof<bool>; PropertyValue = World.getGroupDestroying group world })
        GroupGetters.Add ("ScriptFrame", fun group world -> { PropertyType = typeof<Scripting.ProceduralFrame list>; PropertyValue = World.getGroupScriptFrame group world })
        GroupGetters.Add ("Order", fun group world -> { PropertyType = typeof<int64>; PropertyValue = World.getGroupOrder group world })
        GroupGetters.Add ("Id", fun group world -> { PropertyType = typeof<Guid>; PropertyValue = World.getGroupId group world })
        GroupGetters.Add ("Name", fun group world -> { PropertyType = typeof<string>; PropertyValue = World.getGroupName group world })

    /// Initialize property setters.
    let private initSetters () =
        GroupSetters.Add ("Model", fun property group world -> World.setGroupModelProperty { DesignerType = property.PropertyType; DesignerValue = property.PropertyValue } group world)
        GroupSetters.Add ("Visible", fun property group world -> World.setGroupVisible (property.PropertyValue :?> bool) group world)
        GroupSetters.Add ("Persistent", fun property group world -> World.setGroupPersistent (property.PropertyValue :?> bool) group world)

    /// Initialize getters and setters
    let internal init () =
        initGetters ()
        initSetters ()