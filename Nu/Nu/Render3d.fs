﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.Numerics
open Nu

// 3d rendering implemented with OpenGL 4.1 (or perhaps 4.3+ if we want to do something special for Mac).
// Deferred, PBR, SSAO (or some variant such as SSDO), Variance Shadow Map (Non-Cascading for now), w/ FSAA.
// In addition, I'll at some point implement spheroidal reflection probes, and light probes as well for emissive light.
// Not sure if I'll also implement a full scene mirror render pass for large water body reflection as that's quite expensive.
// Additionally, there appears to be a bias constant that can be used with VSMs to fix up light leaks, so consider that.

/// Describes the material of a 3d surface.
type Material =
    interface // same derived type indicates two materials can potentially be batched
        inherit IComparable // CompareTo of 0 indicates two materials can be drawn in the same batch with the same parameters
        abstract Bounds : Box3 // allows for z-sorting of translucent surfaces
        abstract Transparent : bool // can affect order in which materials are drawn such as in deferred rendering and may disallow batching
        abstract RenderMany : Material array * Matrix4x4 byref * Matrix4x4 byref * Vector3 * Vector3 * Vector3 * Renderer -> unit // does actual batched opengl calls
        end

/// A collection of materials to render in a pass.
type Materials =
    { MaterialsOpaque : Map<Material, Material array>
      MaterialsTransparent : Material array }

/// A message to the 3d renderer.
type [<NoEquality; NoComparison>] RenderMessage3d =
    | MaterialDescriptor of Material
    | MaterialsDescriptor of Material array
    | RenderCallback3d of (Matrix4x4 * Matrix4x4 * Vector3 * Vector3 * Vector3 * Renderer -> unit)
    | RenderPass of (Materials * Matrix4x4 * Matrix4x4 * Vector3 * Vector3 * Vector3 * Renderer -> unit)