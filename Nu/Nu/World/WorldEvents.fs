﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.Numerics
open Prime
open Nu

/// The data for a mouse move event.
type [<StructuralEquality; NoComparison>] MouseMoveData =
    { Position : Vector2 }

/// The data for a mouse button event.
type [<StructuralEquality; NoComparison>] MouseButtonData =
    { Position : Vector2
      Button : MouseButton
      Down : bool }

/// The data for a keyboard key event.
type [<StructuralEquality; NoComparison>] KeyboardKeyData =
    { KeyboardKey : KeyboardKey
      Repeated : bool
      Down : bool }

/// The data for a gamepad button event.
type [<StructuralEquality; NoComparison>] GamepadDirectionData =
    { GamepadDirection : GamepadDirection }

/// The data for a gamepad button event.
type [<StructuralEquality; NoComparison>] GamepadButtonData =
    { GamepadButton : GamepadButton
      Down : bool }

/// The data of a body transform event.
type [<StructuralEquality; NoComparison>] TransformData =
    { BodySource : BodySource
      Position : Vector3
      Rotation : Quaternion }

/// The data for a collision event.
type [<StructuralEquality; NoComparison>] BodyCollisionData =
    { BodyCollider : BodyShapeSource
      BodyCollidee : BodyShapeSource
      Normal : Vector3
      Speed : single }

/// The data for a separation event.
type [<StructuralEquality; NoComparison>] BodySeparationData =
    { BodySeparator : BodyShapeSource
      BodySeparatee : BodyShapeSource }

/// The data for a life cycle event.
type [<StructuralEquality; NoComparison>] LifeCycleData =
    | RegisterData of Simulant
    | UnregisteringData of Simulant
    | MountOptChangeData of Entity Relation option * Entity Relation option * Entity

[<RequireQualifiedAccess>]
module Events =

    let Wildcard = Prime.Events.Wildcard
    let Register = stoa<unit> "Register/Event"
    let Unregistering = stoa<unit> "Unregistering/Event"
    let Change propertyName = stoa<ChangeData> ("Change/" + propertyName + "/Event")
    let LifeCycle simulantTypeName = stoa<LifeCycleData> ("LifeCycle/" + simulantTypeName + "/Event")
    let Update = stoa<unit> "Update/Event"
    let PostUpdate = stoa<unit> "PostUpdate/Event"
    let Render = stoa<unit> "Render/Event"
    let Select = stoa<unit> "Select/Event"
    let Deselecting = stoa<unit> "Deselecting/Event"
    let BodyAdding = stoa<PhysicsId> "Body/Adding/Event"
    let BodyRemoving = stoa<PhysicsId> "Body/Removing/Event"
    let BodyCollision = stoa<BodyCollisionData> "BodyCollision/Event"
    let BodySeparation = stoa<BodySeparationData> "BodySeparation/Event"
    let Click = stoa<unit> "Click/Event"
    let Down = stoa<unit> "Down/Event"
    let Up = stoa<unit> "Up/Event"
    let Toggle = stoa<bool> "Toggle/Event"
    let Toggled = stoa<unit> "Toggled/Event"
    let Untoggled = stoa<unit> "Untoggled/Event"
    let Dial = stoa<bool> "Dial/Event"
    let Dialed = stoa<unit> "Dialed/Event"
    let Undialed = stoa<unit> "Undialed/Event"
    let Touch = stoa<Vector2> "Touch/Event"
    let Touching = stoa<Vector2> "Touching/Event"
    let Untouch = stoa<Vector2> "Untouch/Event"
    let IncomingStart = stoa<unit> "Incoming/Start/Event"
    let IncomingFinish = stoa<unit> "Incoming/Finish/Event"
    let OutgoingStart = stoa<unit> "Outgoing/Start/Event"
    let OutgoingFinish = stoa<unit> "Outgoing/Finish/Event"
    let MouseMove = stoa<MouseMoveData> "Mouse/Move/Event"
    let MouseDrag = stoa<MouseMoveData> "Mouse/Drag/Event"
    let MouseLeftChange = stoa<MouseButtonData> "Mouse/Left/Change/Event"
    let MouseLeftDown = stoa<MouseButtonData> "Mouse/Left/Down/Event"
    let MouseLeftUp = stoa<MouseButtonData> "Mouse/Left/Up/Event"
    let MouseCenterChange = stoa<MouseButtonData> "Mouse/Center/Change/Event"
    let MouseCenterDown = stoa<MouseButtonData> "Mouse/Center/Down/Event"
    let MouseCenterUp = stoa<MouseButtonData> "Mouse/Center/Up/Event"
    let MouseRightChange = stoa<MouseButtonData> "Mouse/Right/Change/Event"
    let MouseRightDown = stoa<MouseButtonData> "Mouse/Right/Down/Event"
    let MouseRightUp = stoa<MouseButtonData> "Mouse/Right/Up/Event"
    let MouseX1Change = stoa<MouseButtonData> "Mouse/X1/Change/Event"
    let MouseX1Down = stoa<MouseButtonData> "Mouse/X1/Down/Event"
    let MouseX1Up = stoa<MouseButtonData> "Mouse/X1/Up/Event"
    let MouseX2Change = stoa<MouseButtonData> "Mouse/X2/Change/Event"
    let MouseX2Down = stoa<MouseButtonData> "Mouse/X2/Down/Event"
    let MouseX2Up = stoa<MouseButtonData> "Mouse/X2/Up/Event"
    let KeyboardKeyChange = stoa<KeyboardKeyData> "KeyboardKey/Change/Event"
    let KeyboardKeyDown = stoa<KeyboardKeyData> "KeyboardKey/Down/Event"
    let KeyboardKeyUp = stoa<KeyboardKeyData> "KeyboardKey/Up/Event"
    let GamepadDirectionChange (index : int) = stoa<GamepadDirectionData> $ "Gamepad/Direction/" + scstring index + "/Change/Event"
    let GamepadButtonChange (index : int) = stoa<GamepadButtonData> $ "Gamepad/Button/" + scstring index + "/Change/Event"
    let GamepadButtonDown (index : int) = stoa<GamepadButtonData> $ "Gamepad/Button/" + scstring index + "/Down/Event"
    let GamepadButtonUp (index : int) = stoa<GamepadButtonData> $ "Gamepad/Button/" + scstring index + "/Up/Event"
    let AssetsReload = stoa<unit> "Assets/Reload/Event"