﻿namespace Nu
open System
open SDL2
open OpenTK
open TiledSharp
open Nu.Core
open Nu.Voords
open Nu.Constants
open Nu.Sdl
open Nu.Audio
open Nu.Rendering
open Nu.Physics
open Nu.Metadata
open Nu.Entities
open Nu.Groups
open Nu.Screens
open Nu.Games
open Nu.Sim
open Nu.OmniBlade
module Program =

    (* WISDOM: Program types and behavior should be closed where possible and open where necessary. *)

    (* WISDOM: From benchmarks. it looks like our mobile target will cost us anywhere from a 75% to 90%
    decrease in speed as compared to the dev machine. However, this can be mitigated in a few ways with
    approximate speed-ups -

    2x gain - Run app at 30fps instead of 60
    2x gain - put physics in another process
    1.5x gain - clip draw calls
    1.5x gain - put rendering in another process, perhaps with physics, and / or render with OpenGL directly
    1.3x gain - store loaded assets in a Dictionary<Dictionary, ...>> rather than a Map<Map, ...>>, or...
    1.3x gain - alternatively, use short-term memoization with a temporary dictionary to cache asset queries during rendering / playing / etc.
    1.2x gain - optimize locality of address usage
    1.2x gain - render tiles layers to their own buffer so that each whole layer can be blitted directly with a single draw call (though this might cause overdraw).
    ? gain - avoid rendering clear tiles! *)

    let [<EntryPoint>] main _ =
        initTypeConverters ()
        let sdlViewConfig = NewWindow { WindowTitle = "OmniBlade"; WindowX = 32; WindowY = 32; WindowFlags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN }
        let sdlRenderFlags = enum<SDL.SDL_RendererFlags> (int SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED ||| int SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC)
        let sdlConfig = makeSdlConfig sdlViewConfig VirtualResolutionX VirtualResolutionY sdlRenderFlags 1024
        run
            (fun sdlDeps -> tryCreateOmniBladeWorld sdlDeps ())
            (fun world -> updateTransition (fun world' -> (true, world')) world)
            sdlConfig

    (*moduleProgram
    open System
    open Propagate

    type Data =
      { A : int
        B : byte }

    type DataRecording =
        | ARecording of int
        | BRecording of byte

    let setA setter =
        ((fun data -> let a2 = setter data.A in { data with A = a2 }),
         (fun data -> ARecording data.A))

    let setB setter =
        ((fun data -> let b2 = setter data.B in { data with B = b2 }),
         (fun data -> BRecording data.B))

    let [<EntryPoint>] main _ =

        Console.WriteLine (
            propagate 0 >.
            plus 2 >.
            mul 5)

        let propagatedData =
            propagate { A = 0; B = 0uy } >>.
            setA incI >>.
            setB incUy*)
