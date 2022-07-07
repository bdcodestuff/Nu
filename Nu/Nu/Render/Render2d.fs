﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.Collections.Generic
open System.IO
open System.Numerics
open System.Runtime.InteropServices
open SDL2
open TiledSharp
open Prime
open Nu

/// Describes what to render.
type [<NoEquality; NoComparison>] RenderDescriptor2d =
    | SpriteDescriptor of SpriteDescriptor
    | SpritesDescriptor of SpritesDescriptor
    | SpritesSegmentedDescriptor of SegmentedSpritesDescriptor
    | CachedSpriteDescriptor of CachedSpriteDescriptor
    | ParticlesDescriptor of ParticlesDescriptor
    | TilesDescriptor of TilesDescriptor
    | TextDescriptor of TextDescriptor
    | RenderCallbackDescriptor2d of (Vector2 * Vector2 * Renderer2d -> unit)

/// A layered message to the 2d rendering system.
/// NOTE: mutation is used only for internal sprite descriptor caching.
and [<NoEquality; NoComparison>] RenderLayeredMessage2d =
    { mutable Elevation : single
      mutable Horizon : single
      mutable AssetTag : obj AssetTag
      mutable RenderDescriptor2d : RenderDescriptor2d }

/// A message to the 2d rendering system.
and [<NoEquality; NoComparison>] RenderMessage2d =
    | RenderLayeredMessage2d of RenderLayeredMessage2d
    | HintRenderPackageUseMessage2d of string
    | HintRenderPackageDisuseMessage2d of string
    | ReloadRenderAssetsMessage2d

/// The 2d renderer. Represents the 2d rendering system in Nu generally.
and Renderer2d =
    inherit Renderer
    /// The sprite batch operational environment if it exists for this implementation.
    abstract SpriteBatchEnvOpt : OpenGL.SpriteBatch.SpriteBatchEnv option
    /// Render a frame of the game.
    abstract Render : Vector2 -> Vector2 -> Vector2i -> RenderMessage2d List -> unit
    /// Swap a rendered frame of the game.
    abstract Swap : unit -> unit
    /// Handle render clean up by freeing all loaded render assets.
    abstract CleanUp : unit -> unit

/// The mock implementation of Renderer2d.
type [<ReferenceEquality; NoComparison>] MockRenderer2d =
    private
        { MockRenderer2d : unit }

    interface Renderer2d with
        member renderer.SpriteBatchEnvOpt = None
        member renderer.Render _ _ _ _ = ()
        member renderer.Swap () = ()
        member renderer.CleanUp () = ()

    static member make () =
        { MockRenderer2d = () }

/// Compares layered 2d render messages.
type RenderLayeredMessage2dComparer () =
    interface IComparer<RenderLayeredMessage2d> with
        member this.Compare (left, right) =
            if left.Elevation < right.Elevation then -1
            elif left.Elevation > right.Elevation then 1
            elif left.Horizon > right.Horizon then -1
            elif left.Horizon < right.Horizon then 1
            else
                let assetNameCompare = strCmp left.AssetTag.AssetName right.AssetTag.AssetName
                if assetNameCompare <> 0 then assetNameCompare
                else strCmp left.AssetTag.PackageName right.AssetTag.PackageName

/// The OpenGL implementation of Renderer2d.
type [<ReferenceEquality; NoComparison>] GlRenderer2d =
    private
        { RenderWindow : Window
          RenderSpriteShader : int * int * int * int * uint
          RenderSpriteQuad : uint * uint * uint
          RenderTextQuad : uint * uint * uint
          mutable RenderSpriteBatchEnv : OpenGL.SpriteBatch.SpriteBatchEnv
          RenderPackages : RenderAsset Packages
          mutable RenderPackageCachedOpt : string * Dictionary<string, RenderAsset> // OPTIMIZATION: nullable for speed
          mutable RenderAssetCachedOpt : string * RenderAsset
          RenderLayeredMessages : RenderLayeredMessage2d List
          RenderShouldBeginFrame : bool
          RenderShouldEndFrame : bool }

    static member private invalidateCaches renderer =
        renderer.RenderPackageCachedOpt <- Unchecked.defaultof<_>
        renderer.RenderAssetCachedOpt <- Unchecked.defaultof<_>

    static member private freeRenderAsset renderAsset renderer =
        GlRenderer2d.invalidateCaches renderer
        match renderAsset with
        | TextureAsset (_, texture) -> OpenGL.Texture.DeleteTexture texture
        | FontAsset (_, font) -> SDL_ttf.TTF_CloseFont font

    static member private tryLoadRenderAsset (asset : obj Asset) renderer =
        GlRenderer2d.invalidateCaches renderer
        match Path.GetExtension asset.FilePath with
        | ".bmp"
        | ".png"
        | ".tif" ->
            match OpenGL.Texture.TryCreateTexture2dUnfiltered asset.FilePath with
            | Right texture ->
                Some (asset.AssetTag.AssetName, TextureAsset texture)
            | Left error ->
                Log.debug ("Could not load texture '" + asset.FilePath + "' due to '" + error + "'.")
                None
        | ".ttf" ->
            let fileFirstName = Path.GetFileNameWithoutExtension asset.FilePath
            let fileFirstNameLength = String.length fileFirstName
            if fileFirstNameLength >= 3 then
                let fontSizeText = fileFirstName.Substring(fileFirstNameLength - 3, 3)
                match Int32.TryParse fontSizeText with
                | (true, fontSize) ->
                    let fontOpt = SDL_ttf.TTF_OpenFont (asset.FilePath, fontSize)
                    if fontOpt <> IntPtr.Zero then Some (asset.AssetTag.AssetName, FontAsset (fontSize, fontOpt))
                    else Log.debug ("Could not load font due to unparsable font size in file name '" + asset.FilePath + "'."); None
                | (false, _) -> Log.debug ("Could not load font due to file name being too short: '" + asset.FilePath + "'."); None
            else Log.debug ("Could not load font '" + asset.FilePath + "'."); None
        | _ -> None

    static member private tryLoadRenderPackage packageName renderer =
        match AssetGraph.tryMakeFromFile Assets.Global.AssetGraphFilePath with
        | Right assetGraph ->
            match AssetGraph.tryLoadAssetsFromPackage true (Some Constants.Associations.Render2d) packageName assetGraph with
            | Right assets ->
                let renderAssetOpts = List.map (fun asset -> GlRenderer2d.tryLoadRenderAsset asset renderer) assets
                let renderAssets = List.definitize renderAssetOpts
                match Dictionary.tryFind packageName renderer.RenderPackages with
                | Some renderAssetDict ->
                    for (key, value) in renderAssets do renderAssetDict.Assign (key, value)
                    renderer.RenderPackages.Assign (packageName, renderAssetDict)
                | None ->
                    let renderAssetDict = dictPlus StringComparer.Ordinal renderAssets
                    renderer.RenderPackages.Assign (packageName, renderAssetDict)
            | Left failedAssetNames ->
                Log.info ("Render package load failed due to unloadable assets '" + failedAssetNames + "' for package '" + packageName + "'.")
        | Left error ->
            Log.info ("Render package load failed due to unloadable asset graph due to: '" + error)

    static member tryFindRenderAsset (assetTag : obj AssetTag) renderer =
        if  renderer.RenderPackageCachedOpt :> obj |> notNull &&
            fst renderer.RenderPackageCachedOpt = assetTag.PackageName then
            if  renderer.RenderAssetCachedOpt :> obj |> notNull &&
                fst renderer.RenderAssetCachedOpt = assetTag.AssetName then
                ValueSome (snd renderer.RenderAssetCachedOpt)
            else
                let assets = snd renderer.RenderPackageCachedOpt
                match assets.TryGetValue assetTag.AssetName with
                | (true, asset) ->
                    renderer.RenderAssetCachedOpt <- (assetTag.AssetName, asset)
                    ValueSome asset
                | (false, _) -> ValueNone
        else
            match Dictionary.tryFind assetTag.PackageName renderer.RenderPackages with
            | Some assets ->
                renderer.RenderPackageCachedOpt <- (assetTag.PackageName, assets)
                match assets.TryGetValue assetTag.AssetName with
                | (true, asset) ->
                    renderer.RenderAssetCachedOpt <- (assetTag.AssetName, asset)
                    ValueSome asset
                | (false, _) -> ValueNone
            | None ->
                Log.info ("Loading render package '" + assetTag.PackageName + "' for asset '" + assetTag.AssetName + "' on the fly.")
                GlRenderer2d.tryLoadRenderPackage assetTag.PackageName renderer
                match renderer.RenderPackages.TryGetValue assetTag.PackageName with
                | (true, assets) ->
                    renderer.RenderPackageCachedOpt <- (assetTag.PackageName, assets)
                    match assets.TryGetValue assetTag.AssetName with
                    | (true, asset) ->
                        renderer.RenderAssetCachedOpt <- (assetTag.AssetName, asset)
                        ValueSome asset
                    | (false, _) -> ValueNone
                | (false, _) -> ValueNone

    static member private handleHintRenderPackageUse hintPackageName renderer =
        GlRenderer2d.tryLoadRenderPackage hintPackageName renderer

    static member private handleHintRenderPackageDisuse hintPackageName renderer =
        match Dictionary.tryFind hintPackageName renderer.RenderPackages with
        | Some assets ->
            for asset in assets do GlRenderer2d.freeRenderAsset asset.Value renderer
            renderer.RenderPackages.Remove hintPackageName |> ignore
        | None -> ()

    static member private handleReloadRenderAssets renderer =
        let packageNames = renderer.RenderPackages |> Seq.map (fun entry -> entry.Key) |> Array.ofSeq
        renderer.RenderPackages.Clear ()
        for packageName in packageNames do
            GlRenderer2d.tryLoadRenderPackage packageName renderer

    static member private handleRenderMessage renderMessage renderer =
        match renderMessage with
        | RenderLayeredMessage2d message -> renderer.RenderLayeredMessages.Add message
        | HintRenderPackageUseMessage2d hintPackageUse -> GlRenderer2d.handleHintRenderPackageUse hintPackageUse renderer
        | HintRenderPackageDisuseMessage2d hintPackageDisuse -> GlRenderer2d.handleHintRenderPackageDisuse hintPackageDisuse renderer
        | ReloadRenderAssetsMessage2d -> GlRenderer2d.handleReloadRenderAssets renderer

    static member private handleRenderMessages renderMessages renderer =
        for renderMessage in renderMessages do
            GlRenderer2d.handleRenderMessage renderMessage renderer

    static member private sortRenderLayeredMessages renderer =
        renderer.RenderLayeredMessages.Sort (RenderLayeredMessage2dComparer ())

    /// Compute the 2d absolute view matrix.
    static member computeViewAbsolute (_ : Vector2) (eyeSize : Vector2) (_ : GlRenderer2d) =
        let translation = eyeSize * 0.5f * Constants.Render.VirtualScalar2
        Matrix4x4.CreateTranslation (v3 translation.X translation.Y 1.0f)

    /// Compute the 2d relative view matrix.
    static member computeViewRelative (eyePosition : Vector2) (eyeSize : Vector2) (_ : GlRenderer2d) =
        let translation = -eyePosition * Constants.Render.VirtualScalar2 + eyeSize * 0.5f * Constants.Render.VirtualScalar2
        Matrix4x4.CreateTranslation (v3 translation.X translation.Y 1.0f)

    /// Compute the 2d projection matrix.
    static member computeProjection (viewport : Box2i) (_ : GlRenderer2d) =
        Matrix4x4.CreateOrthographicOffCenter
            (single (viewport.Position.X),
             single (viewport.Position.X + viewport.Size.X),
             single (viewport.Position.Y),
             single (viewport.Position.Y + viewport.Size.Y),
             -1.0f, 1.0f)

    /// Get the sprite shader created by OpenGL.Hl.CreateSpriteShader.
    static member getSpriteShader renderer =
        renderer.RenderSpriteShader

    /// Get the sprite quad created by OpenGL.Hl.CreateSpriteQuad.
    static member getSpriteQuad renderer =
        renderer.RenderSpriteQuad

    /// Get the text quad created by OpenGL.Hl.CreateSpriteQuad.
    static member getTextQuad renderer =
        renderer.RenderTextQuad

    static member inline private batchSprite
        absolute
        position
        size
        pivot
        rotation
        (insetOpt : Box2 voption)
        (textureMetadata : OpenGL.Texture.TextureMetadata)
        texture
        (color : Color)
        blend
        (glow : Color)
        flip
        renderer =

        // compute unflipped tex coords
        let texCoordsUnflipped =
            match insetOpt with
            | ValueSome inset ->
                let texelWidth = textureMetadata.TextureTexelWidth
                let texelHeight = textureMetadata.TextureTexelHeight
                let borderWidth = texelWidth * Constants.Render.SpriteBorderTexelScalar
                let borderHeight = texelHeight * Constants.Render.SpriteBorderTexelScalar
                let px = inset.Position.X * texelWidth + borderWidth
                let py = (inset.Position.Y + inset.Size.Y) * texelHeight - borderHeight
                let sx = inset.Size.X * texelWidth - borderWidth * 2.0f
                let sy = -inset.Size.Y * texelHeight + borderHeight * 2.0f
                Box2 (px, py, sx, sy)
            | ValueNone -> Box2 (0.0f, 1.0f, 1.0f, -1.0f)
            
        // compute a flipping flags
        let struct (flipH, flipV) =
            match flip with
            | FlipNone -> struct (false, false)
            | FlipH -> struct (true, false)
            | FlipV -> struct (false, true)
            | FlipHV -> struct (true, true)

        // compute tex coords
        let texCoords =
            box2
                (v2
                    (if flipH then texCoordsUnflipped.Position.X + texCoordsUnflipped.Size.X else texCoordsUnflipped.Position.X)
                    (if flipV then texCoordsUnflipped.Position.Y + texCoordsUnflipped.Size.Y else texCoordsUnflipped.Position.Y))
                (v2
                    (if flipH then -texCoordsUnflipped.Size.X else texCoordsUnflipped.Size.X)
                    (if flipV then -texCoordsUnflipped.Size.Y else texCoordsUnflipped.Size.Y))

        // compute blending instructions
        let struct (bfs, bfd, beq) =
            match blend with
            | Transparent -> struct (OpenGL.BlendingFactor.SrcAlpha, OpenGL.BlendingFactor.OneMinusSrcAlpha, OpenGL.BlendEquationMode.FuncAdd)
            | Additive -> struct (OpenGL.BlendingFactor.SrcAlpha, OpenGL.BlendingFactor.One, OpenGL.BlendEquationMode.FuncAdd)
            | Overwrite -> struct (OpenGL.BlendingFactor.One, OpenGL.BlendingFactor.Zero, OpenGL.BlendEquationMode.FuncAdd)

        // attempt to draw normal sprite
        if color.A <> 0.0f then
            OpenGL.SpriteBatch.SubmitSpriteBatchSprite (absolute, position, size, pivot, rotation, &texCoords, &color, bfs, bfd, beq, texture, renderer.RenderSpriteBatchEnv)

        // attempt to draw glow sprite
        if glow.A <> 0.0f then
            OpenGL.SpriteBatch.SubmitSpriteBatchSprite (absolute, position, size, pivot, rotation, &texCoords, &glow, OpenGL.BlendingFactor.SrcAlpha, OpenGL.BlendingFactor.One, OpenGL.BlendEquationMode.FuncAdd, texture, renderer.RenderSpriteBatchEnv)

    /// Render sprite.
    static member renderSprite
        (transform : Transform byref,
         insetOpt : Box2 ValueOption inref,
         image : Image AssetTag,
         color : Color inref,
         blend : Blend,
         glow : Color inref,
         flip : Flip,
         renderer) =
        let absolute = transform.Absolute
        let perimeter = transform.Perimeter
        let position = perimeter.Position.V2 * Constants.Render.VirtualScalar2
        let size = perimeter.Size.V2 * Constants.Render.VirtualScalar2
        let pivot = transform.Pivot.V2 * Constants.Render.VirtualScalar2
        let rotation = transform.Angles.Z
        let image = AssetTag.generalize image
        match GlRenderer2d.tryFindRenderAsset image renderer with
        | ValueSome renderAsset ->
            match renderAsset with
            | TextureAsset (textureMetadata, texture) ->
                GlRenderer2d.batchSprite absolute position size pivot rotation insetOpt textureMetadata texture color blend glow flip renderer
            | _ -> Log.trace "Cannot render sprite with a non-texture asset."
        | _ -> Log.info ("SpriteDescriptor failed to render due to unloadable assets for '" + scstring image + "'.")

    /// Render particles.
    static member renderParticles (blend : Blend, image : Image AssetTag, particles : Particle SegmentedArray, renderer) =
        let image = AssetTag.generalize image
        match GlRenderer2d.tryFindRenderAsset image renderer with
        | ValueSome renderAsset ->
            match renderAsset with
            | TextureAsset (textureMetadata, texture) ->
                let mutable index = 0
                while index < particles.Length do
                    let particle = &particles.[index]
                    let transform = &particle.Transform
                    let absolute = transform.Absolute
                    let perimeter = transform.Perimeter
                    let position = perimeter.Position.V2 * Constants.Render.VirtualScalar2
                    let size = perimeter.Size.V2 * Constants.Render.VirtualScalar2
                    let pivot = transform.Pivot.V2 * Constants.Render.VirtualScalar2
                    let rotation = transform.Angles.Z
                    let color = &particle.Color
                    let glow = &particle.Glow
                    let flip = particle.Flip
                    let insetOpt = &particle.InsetOpt
                    GlRenderer2d.batchSprite absolute position size pivot rotation insetOpt textureMetadata texture color blend glow flip renderer
                    index <- inc index
            | _ -> Log.trace "Cannot render particle with a non-texture asset."
        | _ -> Log.info ("RenderDescriptors failed to render due to unloadable assets for '" + scstring image + "'.")

    /// Render tiles.
    static member renderTiles
        (transform : Transform byref,
         color : Color inref,
         glow : Color inref,
         mapSize : Vector2i,
         tiles : TmxLayerTile array,
         tileSourceSize : Vector2i,
         tileSize : Vector2,
         tileAssets : (TmxTileset * Image AssetTag) array,
         eyePosition : Vector2,
         eyeSize : Vector2,
         renderer) =
        let absolute = transform.Absolute
        let perimeter = transform.Perimeter
        let position = perimeter.Position.V2 * Constants.Render.VirtualScalar2
        let size = perimeter.Size.V2 * Constants.Render.VirtualScalar2
        let eyePosition = eyePosition * Constants.Render.VirtualScalar2
        let eyeSize = eyeSize * Constants.Render.VirtualScalar2
        let tileSize = tileSize * Constants.Render.VirtualScalar2
        let tilePivot = tileSize * 0.5f // just rotate around center
        let (allFound, tileSetTextures) =
            tileAssets |>
            Array.map (fun (tileSet, tileSetImage) ->
                match GlRenderer2d.tryFindRenderAsset (AssetTag.generalize tileSetImage) renderer with
                | ValueSome (TextureAsset (tileSetTexture, tileSetTextureMetadata)) -> Some (tileSet, tileSetImage, tileSetTexture, tileSetTextureMetadata)
                | ValueSome _ -> None
                | ValueNone -> None) |>
            Array.definitizePlus
        if allFound then
            // OPTIMIZATION: allocating refs in a tight-loop is problematic, so pulled out here
            let tilesLength = Array.length tiles
            let mutable tileIndex = 0
            while tileIndex < tilesLength do
                let tile = &tiles.[tileIndex]
                if tile.Gid <> 0 then // not the empty tile
                    let mapRun = mapSize.X
                    let (i, j) = (tileIndex % mapRun, tileIndex / mapRun)
                    let tilePosition =
                        v2
                            (position.X + tileSize.X * single i)
                            (position.Y - tileSize.Y - tileSize.Y * single j + size.Y)
                    let tileBounds = box2 tilePosition tileSize
                    let viewBounds = box2 (eyePosition - eyeSize * 0.5f) eyeSize
                    if Math.isBoundsIntersectingBounds2d tileBounds viewBounds then
        
                        // compute tile flip
                        let flip =
                            match (tile.HorizontalFlip, tile.VerticalFlip) with
                            | (false, false) -> FlipNone
                            | (true, false) -> FlipH
                            | (false, true) -> FlipV
                            | (true, true) -> FlipHV
        
                        // attempt to compute tile set texture
                        let mutable tileOffset = 1 // gid 0 is the empty tile
                        let mutable tileSetIndex = 0
                        let mutable tileSetWidth = 0
                        let mutable tileSetTextureOpt = None
                        for (set, _, textureMetadata, texture) in tileSetTextures do
                            let tileCountOpt = set.TileCount
                            let tileCount = if tileCountOpt.HasValue then tileCountOpt.Value else 0
                            if  tile.Gid >= set.FirstGid && tile.Gid < set.FirstGid + tileCount ||
                                not tileCountOpt.HasValue then // HACK: when tile count is missing, assume we've found the tile...?
                                tileSetWidth <- let tileSetWidthOpt = set.Image.Width in tileSetWidthOpt.Value
                                tileSetTextureOpt <- Some (textureMetadata, texture)
                            if Option.isNone tileSetTextureOpt then
                                tileSetIndex <- inc tileSetIndex
                                tileOffset <- tileOffset + tileCount
        
                        // attempt to render tile
                        match tileSetTextureOpt with
                        | Some (textureMetadata, texture) ->
                            let tileId = tile.Gid - tileOffset
                            let tileIdPosition = tileId * tileSourceSize.X
                            let tileSourcePosition = v2 (single (tileIdPosition % tileSetWidth)) (single (tileIdPosition / tileSetWidth * tileSourceSize.Y))
                            let inset = box2 tileSourcePosition (v2 (single tileSourceSize.X) (single tileSourceSize.Y))
                            GlRenderer2d.batchSprite absolute tilePosition tileSize tilePivot 0.0f (ValueSome inset) textureMetadata texture color Transparent glow flip renderer
                        | None -> ()

                tileIndex <- inc tileIndex
        else Log.info ("TileLayerDescriptor failed due to unloadable or non-texture assets for one or more of '" + scstring tileAssets + "'.")

    /// Render text.
    static member renderText
        (transform : Transform byref,
         text : string,
         font : Font AssetTag,
         color : Color inref,
         justification : Justification,
         eyePosition : Vector2,
         eyeSize : Vector2,
         renderer : GlRenderer2d) =
        let (transform, color) = (transform, color) // make visible from lambda
        flip OpenGL.SpriteBatch.InterruptSpriteBatchFrame renderer.RenderSpriteBatchEnv $ fun () ->
            let mutable transform = transform
            let absolute = transform.Absolute
            let perimeter = transform.Perimeter
            let position = perimeter.Position.V2 * Constants.Render.VirtualScalar2
            let size = perimeter.Size.V2 * Constants.Render.VirtualScalar2
            let viewport = Constants.Render.Viewport
            let viewAbsolute = GlRenderer2d.computeViewAbsolute eyePosition eyeSize renderer
            let viewRelative = GlRenderer2d.computeViewRelative eyePosition eyeSize renderer
            let projection = GlRenderer2d.computeProjection viewport renderer
            let viewProjection = if absolute then viewAbsolute * projection else viewRelative * projection
            let font = AssetTag.generalize font
            match GlRenderer2d.tryFindRenderAsset font renderer with
            | ValueSome renderAsset ->
                match renderAsset with
                | FontAsset (_, font) ->

                    // gather rendering resources
                    // NOTE: the resource implications (throughput and fragmentation?) of creating and destroying a
                    // surface and texture one or more times a frame must be understood!
                    let (offset, textSurface, textSurfacePtr) =

                        // create sdl color
                        let mutable colorSdl = SDL.SDL_Color ()
                        colorSdl.r <- color.R8
                        colorSdl.g <- color.G8
                        colorSdl.b <- color.B8
                        colorSdl.a <- color.A8

                        // get text metrics
                        let mutable width = 0
                        let mutable height = 0
                        SDL_ttf.TTF_SizeText (font, text, &width, &height) |> ignore
                        let width = single (width * Constants.Render.VirtualScalar)
                        let height = single (height * Constants.Render.VirtualScalar)

                        // justify
                        match justification with
                        | Unjustified wrapped ->
                            let textSurfacePtr =
                                if wrapped
                                then SDL_ttf.TTF_RenderText_Blended_Wrapped (font, text, colorSdl, uint32 size.X)
                                else SDL_ttf.TTF_RenderText_Blended (font, text, colorSdl)
                            let textSurface = Marshal.PtrToStructure<SDL.SDL_Surface> textSurfacePtr
                            let textSurfaceHeight = single (textSurface.h * Constants.Render.VirtualScalar)
                            let offsetY = size.Y - textSurfaceHeight
                            (v2 0.0f offsetY, textSurface, textSurfacePtr)
                        | Justified (h, v) ->
                            let textSurfacePtr = SDL_ttf.TTF_RenderText_Blended (font, text, colorSdl)
                            let textSurface = Marshal.PtrToStructure<SDL.SDL_Surface> textSurfacePtr
                            let offsetX =
                                match h with
                                | JustifyLeft -> 0.0f
                                | JustifyCenter -> (size.X - width) * 0.5f
                                | JustifyRight -> size.X - width
                            let offsetY =
                                match v with
                                | JustifyTop -> 0.0f
                                | JustifyMiddle -> (size.Y - height) * 0.5f
                                | JustifyBottom -> size.Y - height
                            let offset = v2 offsetX offsetY
                            (offset, textSurface, textSurfacePtr)

                    if textSurfacePtr <> IntPtr.Zero then

                        // construct mvp matrix
                        let textSurfaceWidth = single (textSurface.w * Constants.Render.VirtualScalar)
                        let textSurfaceHeight = single (textSurface.h * Constants.Render.VirtualScalar)
                        let translation = (position + offset).V3
                        let scale = v3 textSurfaceWidth textSurfaceHeight 1.0f
                        let modelTranslation = Matrix4x4.CreateTranslation translation
                        let modelScale = Matrix4x4.CreateScale scale
                        let modelMatrix = modelScale * modelTranslation
                        let modelViewProjection = modelMatrix * viewProjection

                        // upload texture
                        let textTexture = OpenGL.Gl.GenTexture ()
                        OpenGL.Gl.BindTexture (OpenGL.TextureTarget.Texture2d, textTexture)
                        OpenGL.Gl.TexImage2D (OpenGL.TextureTarget.Texture2d, 0, OpenGL.InternalFormat.Rgba8, textSurface.w, textSurface.h, 0, OpenGL.PixelFormat.Bgra, OpenGL.PixelType.UnsignedByte, textSurface.pixels)
                        OpenGL.Gl.TexParameter (OpenGL.TextureTarget.Texture2d, OpenGL.TextureParameterName.TextureMinFilter, int OpenGL.TextureMinFilter.Nearest)
                        OpenGL.Gl.TexParameter (OpenGL.TextureTarget.Texture2d, OpenGL.TextureParameterName.TextureMagFilter, int OpenGL.TextureMagFilter.Nearest)
                        OpenGL.Gl.BindTexture (OpenGL.TextureTarget.Texture2d, 0u)
                        OpenGL.Hl.Assert ()

                        // draw text sprite
                        // NOTE: we allocate an array here, too.
                        let (vertices, indices, vao) = renderer.RenderTextQuad
                        let (modelViewProjectionUniform, texCoords4Uniform, colorUniform, texUniform, shader) = renderer.RenderSpriteShader
                        OpenGL.Sprite.DrawSprite (vertices, indices, vao, modelViewProjection.ToArray (), ValueNone, Color.White, FlipNone, textSurface.w, textSurface.h, textTexture, modelViewProjectionUniform, texCoords4Uniform, colorUniform, texUniform, shader)
                        OpenGL.Hl.Assert ()

                        // destroy texture
                        OpenGL.Gl.DeleteTextures textTexture
                        OpenGL.Hl.Assert ()

                    SDL.SDL_FreeSurface textSurfacePtr

                | _ -> Log.debug "Cannot render text with a non-font asset."
            | _ -> Log.info ("TextDescriptor failed due to unloadable assets for '" + scstring font + "'.")
        OpenGL.Hl.Assert ()

    static member inline private renderCallback callback eyePosition eyeSize renderer =
        flip OpenGL.SpriteBatch.InterruptSpriteBatchFrame renderer.RenderSpriteBatchEnv $ fun () -> callback (eyePosition, eyeSize, renderer)
        OpenGL.Hl.Assert ()

    static member private renderDescriptor descriptor eyePosition eyeSize renderer =
        match descriptor with
        | SpriteDescriptor descriptor ->
            GlRenderer2d.renderSprite (&descriptor.Transform, &descriptor.InsetOpt, descriptor.Image, &descriptor.Color, descriptor.Blend, &descriptor.Glow, descriptor.Flip, renderer)
        | SpritesDescriptor descriptor ->
            let sprites = descriptor.Sprites
            for index in 0 .. sprites.Length - 1 do
                let sprite = &sprites.[index]
                GlRenderer2d.renderSprite (&sprite.Transform, &sprite.InsetOpt, sprite.Image, &sprite.Color, sprite.Blend, &sprite.Glow, sprite.Flip, renderer)
        | SpritesSegmentedDescriptor descriptor ->
            let sprites = descriptor.SegmentedSprites
            for index in 0 .. sprites.Length - 1 do
                let sprite = &sprites.[index]
                GlRenderer2d.renderSprite (&sprite.Transform, &sprite.InsetOpt, sprite.Image, &sprite.Color, sprite.Blend, &sprite.Glow, sprite.Flip, renderer)
        | CachedSpriteDescriptor descriptor ->
            GlRenderer2d.renderSprite (&descriptor.CachedSprite.Transform, &descriptor.CachedSprite.InsetOpt, descriptor.CachedSprite.Image, &descriptor.CachedSprite.Color, descriptor.CachedSprite.Blend, &descriptor.CachedSprite.Glow, descriptor.CachedSprite.Flip, renderer)
        | ParticlesDescriptor descriptor ->
            GlRenderer2d.renderParticles (descriptor.Blend, descriptor.Image, descriptor.Particles, renderer)
        | TilesDescriptor descriptor ->
            GlRenderer2d.renderTiles
                (&descriptor.Transform, &descriptor.Color, &descriptor.Glow,
                 descriptor.MapSize, descriptor.Tiles, descriptor.TileSourceSize, descriptor.TileSize, descriptor.TileAssets,
                 eyePosition, eyeSize, renderer)
        | TextDescriptor descriptor ->
            GlRenderer2d.renderText
                (&descriptor.Transform, descriptor.Text, descriptor.Font, &descriptor.Color, descriptor.Justification, eyePosition, eyeSize, renderer)
        | RenderCallbackDescriptor2d callback ->
            GlRenderer2d.renderCallback callback eyePosition eyeSize renderer

    static member private renderLayeredMessages eyePosition eyeSize renderer =
        for message in renderer.RenderLayeredMessages do
            GlRenderer2d.renderDescriptor message.RenderDescriptor2d eyePosition eyeSize renderer

    /// Make a GlRenderer2d.
    static member make window config =

        // initialize context if directed
        if config.ShouldInitializeContext then

            // create SDL-OpenGL context if needed
            match window with
            | SglWindow window -> OpenGL.Hl.CreateSglContext window.SglWindow |> ignore<nativeint>
            | WfglWindow _ -> () // TODO: 3D: see if we can make current the GL context here so that threaded OpenGL works in Gaia.
            OpenGL.Hl.Assert ()

            // listen to debug messages
            OpenGL.Hl.AttachDebugMessageCallback ()

        // create one-off sprite and text resources
        let spriteShader = OpenGL.Sprite.CreateSpriteShader ()
        let spriteQuad = OpenGL.Sprite.CreateSpriteQuad false
        let textQuad = OpenGL.Sprite.CreateSpriteQuad true
        OpenGL.Hl.Assert ()

        // create sprite batch env
        let spriteBatchEnv = OpenGL.SpriteBatch.CreateSpriteBatchEnv ()
        OpenGL.Hl.Assert ()

        // make renderer
        let renderer =
            { RenderWindow = window
              RenderSpriteShader = spriteShader
              RenderSpriteQuad = spriteQuad
              RenderTextQuad = textQuad
              RenderSpriteBatchEnv = spriteBatchEnv
              RenderPackages = dictPlus StringComparer.Ordinal []
              RenderPackageCachedOpt = Unchecked.defaultof<_>
              RenderAssetCachedOpt = Unchecked.defaultof<_>
              RenderLayeredMessages = List ()
              RenderShouldBeginFrame = config.ShouldBeginFrame
              RenderShouldEndFrame = config.ShouldEndFrame }

        // fin
        renderer

    interface Renderer2d with

        member renderer.SpriteBatchEnvOpt =
            Some renderer.RenderSpriteBatchEnv

        member renderer.Render eyePosition eyeSize windowSize renderMessages =

            // begin frame
            if renderer.RenderShouldBeginFrame then
                OpenGL.Hl.BeginFrame (Constants.Render.ViewportOffset windowSize)
                OpenGL.Hl.Assert ()

            // begin sprite batch frame
            let viewport = Constants.Render.Viewport
            let projection = GlRenderer2d.computeProjection viewport renderer
            let viewProjectionAbsolute = GlRenderer2d.computeViewAbsolute eyePosition eyeSize renderer * projection
            let viewProjectionRelative = GlRenderer2d.computeViewRelative eyePosition eyeSize renderer * projection
            OpenGL.SpriteBatch.BeginSpriteBatchFrame (&viewProjectionAbsolute, &viewProjectionRelative, renderer.RenderSpriteBatchEnv)
            OpenGL.Hl.Assert ()

            // render frame
            GlRenderer2d.handleRenderMessages renderMessages renderer
            GlRenderer2d.sortRenderLayeredMessages renderer
            GlRenderer2d.renderLayeredMessages eyePosition eyeSize renderer
            renderer.RenderLayeredMessages.Clear ()

            // end sprite batch frame
            OpenGL.SpriteBatch.EndSpriteBatchFrame renderer.RenderSpriteBatchEnv
            OpenGL.Hl.Assert ()

            // end frame
            if renderer.RenderShouldEndFrame then
                OpenGL.Hl.EndFrame ()
                OpenGL.Hl.Assert ()

        member renderer.Swap () =
            match renderer.RenderWindow with
            | SglWindow window -> SDL.SDL_GL_SwapWindow window.SglWindow
            | WfglWindow window -> window.WfglSwapWindow ()

        member renderer.CleanUp () =
            OpenGL.SpriteBatch.DestroySpriteBatchEnv renderer.RenderSpriteBatchEnv
            OpenGL.Hl.Assert ()
            let renderAssetPackages = renderer.RenderPackages |> Seq.map (fun entry -> entry.Value)
            let renderAssets = renderAssetPackages |> Seq.collect (Seq.map (fun entry -> entry.Value))
            for renderAsset in renderAssets do GlRenderer2d.freeRenderAsset renderAsset renderer
            renderer.RenderPackages.Clear ()