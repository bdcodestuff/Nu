﻿// Gaia - The Nu Game Engine editor.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu.Gaia
open System
open Prime
open Nu

[<RequireQualifiedAccess>]
module Constants =

    [<RequireQualifiedAccess>]
    module Editor =
    
        let [<Literal>] PositionSnapDefault = 12
        let [<Literal>] RotationSnapDefault = 5
        let [<Literal>] CreationDepthDefault = 0.0f
        let [<Literal>] CameraSpeed = 3.0f // NOTE: might be nice to be able to configure this just like entity creation depth in the editor
        let [<Literal>] SavedStateFilePath = "GaiaState.txt"
        let [<Literal>] NonePick = "\"None\""