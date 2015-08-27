﻿// NuEdit - The Nu Game Engine editor.
// Copyright (C) Bryan Edds, 2013-2015.

namespace NuEdit
open System
open Prime
open Nu
open NuEdit

[<RequireQualifiedAccess>]
module Simulants =

    let EditorScreen = !> "EditorScreen"
    let EditorGroup = EditorScreen => Constants.Engine.DefaultGroupName