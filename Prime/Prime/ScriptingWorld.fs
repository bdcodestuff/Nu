﻿// Prime - A PRIMitivEs code library.
// Copyright (C) Bryan Edds, 2013-2017.

namespace Prime
open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open Prime
open Prime.Scripting
open Prime.ScriptingUnary
open Prime.ScriptingBinary
open Prime.ScriptingMarshalling
open Prime.ScriptingPrimitives

/// The context in which scripting takes place. Effectively a mix-in for the 'w type, where 'w is a type that
/// represents the client program.
type 'w ScriptingWorld =
    interface
        abstract member GetEnv : unit -> Env
        abstract member UpdateEnv : (Env -> Env) -> 'w
        abstract member UpdateEnvPlus : (Env -> struct ('a * Env)) -> struct ('a * 'w)
        abstract member IsExtrinsic : string -> bool
        abstract member GetExtrinsic : string -> (Expr array -> SymbolOrigin option -> 'w -> struct (Expr * 'w))
        abstract member TryImport : Type -> obj -> Expr option
        abstract member TryExport : Type -> Expr -> obj option
        end

[<RequireQualifiedAccess>]
module ScriptingWorld =

    let inline annotateWorld<'w when 'w :> 'w ScriptingWorld> (_ : 'w) =
        () // NOTE: simply infers that a type is a world.

    let tryGetBinding<'w when 'w :> 'w ScriptingWorld> name cachedBinding (world : 'w) =
        Env.tryGetBinding name cachedBinding (world.GetEnv ())

    let tryAddDeclarationBinding<'w when 'w :> 'w ScriptingWorld> name value (world : 'w) =
        world.UpdateEnvPlus (Env.tryAddDeclarationBinding name value)

    let addProceduralBinding<'w when 'w :> 'w ScriptingWorld> appendType name value (world : 'w) =
        world.UpdateEnv (Env.addProceduralBinding appendType name value)

    let addProceduralBindings<'w when 'w :> 'w ScriptingWorld> appendType bindings (world : 'w) =
        world.UpdateEnv (Env.addProceduralBindings appendType bindings)

    let removeProceduralBindings<'w when 'w :> 'w ScriptingWorld> (world : 'w) =
        world.UpdateEnv (Env.removeProceduralBindings)

    let getProceduralFrames<'w when 'w :> 'w ScriptingWorld> (world : 'w) =
        Env.getProceduralFrames (world.GetEnv ())

    let setProceduralFrames<'w when 'w :> 'w ScriptingWorld> proceduralFrames (world : 'w) =
        world.UpdateEnv (Env.setProceduralFrames proceduralFrames)

    let getGlobalFrame<'w when 'w :> 'w ScriptingWorld> (world : 'w) =
        Env.getGlobalFrame (world.GetEnv ())

    let getLocalFrame<'w when 'w :> 'w ScriptingWorld> (world : 'w) =
        Env.getLocalFrame (world.GetEnv ())

    let setLocalFrame<'w when 'w :> 'w ScriptingWorld> localFrame (world : 'w) =
        world.UpdateEnv (Env.setLocalFrame localFrame)

    let tryImport<'w when 'w :> 'w ScriptingWorld> ty value (world : 'w) =
        tryImport world.TryImport ty value

    let tryExport<'w when 'w :> 'w ScriptingWorld> ty value (world : 'w) =
        tryExport world.TryExport ty value

    let log expr =
        match expr with
        | Violation (names, error, originOpt) ->
            Log.info ^
                "Unexpected Violation: " + String.concat Constants.Scripting.ViolationSeparatorStr names + "\n" +
                "Due to: " + error + "\n" +
                SymbolOrigin.tryPrint originOpt + "\n"
        | _ -> ()

    let rec evalOverload fnName evaledArgs originOpt world =
        if Array.notEmpty evaledArgs then
            match Array.last evaledArgs with
            | Pluggable pluggable ->
                let pluggableTypeName = pluggable.TypeName
                let xfnName = fnName + "_" + pluggableTypeName
                let xfnBinding = Binding (xfnName, ref UncachedBinding, None)
                let evaleds = Array.cons xfnBinding evaledArgs
                evalApply evaleds originOpt world
            | Union (name, _)
            | Record (name, _, _) ->
                let xfnName = fnName + "_" + name
                let xfnBinding = Binding (xfnName, ref UncachedBinding, None)
                let evaleds = Array.cons xfnBinding evaledArgs
                evalApply evaleds originOpt world
            | Violation _ as error -> struct (error, world)
            | _ -> struct (Violation (["InvalidOverload"], "Could not find overload for '" + fnName + "' for target.", originOpt), world)
        else struct (Violation (["InvalidFunctionTargetBinding"], "Cannot apply the non-existent binding '" + fnName + "'.", originOpt), world)

    and getOverload fnName =
        fun evaledArgs originOpt world ->
            evalOverload fnName evaledArgs originOpt world

    and isIntrinsic fnName =
        match fnName with
        | "=" | "<>" | "<" | ">" | "<=" | ">=" | "+" | "-" | "*" | "/" | "%" | "!"
        | "not" | "toEmpty" | "toIdentity" | "toMin" | "toMax"
        | "inc" | "dec" | "negate" | "hash"
        | "pow" | "root" | "sqr" | "sqrt"
        | "floor" | "ceiling" | "truncate" | "round" | "exp" | "log"
        | "sin" | "cos" | "tan" | "asin" | "acos" | "atan"
        | "length" | "normal" | "cross" | "dot"
        | "bool" | "int" | "int64" | "single" | "double" | "string"
        | "getTypeName"
        | "tryIndex" | "hasIndex" | "index" | "tryUpdate" | "update" | "getName"
        | "tuple" | "pair" | "fst" | "snd" | "thd" | "fth" | "fif" | "nth"
        | "fstAs" | "sndAs" | "thdAs" | "fthAs" | "fifAs" | "nthAs"
        | "some" | "isNone" | "isSome" | "isEmpty" | "notEmpty"
        | "tryUncons" | "uncons" | "cons" | "commit" | "tryHead" | "head" | "tryTail" | "tail"
        | "scanWhile" | "scani" | "scan" | "foldWhile" | "foldi" | "fold" | "mapi" | "map" | "contains"
        | "toString"
        | "codata" | "toCodata"
        | "list" | "toList"
        | "ring" | "toRing" | "add" | "remove"
        | "table" | "toTable" -> true
        | _ -> false

    and internal getIntrinsic<'w when 'w :> 'w ScriptingWorld> fnName =
        match fnName with
        | "=" -> fun evaledArgs originOpt (world : 'w) -> evalBinary EqFns fnName evaledArgs originOpt world
        | "<>" -> fun evaledArgs originOpt world -> evalBinary NotEqFns fnName evaledArgs originOpt world
        | "<" -> fun evaledArgs originOpt world -> evalBinary LtFns fnName evaledArgs originOpt world
        | ">" -> fun evaledArgs originOpt world -> evalBinary GtFns fnName evaledArgs originOpt world
        | "<=" -> fun evaledArgs originOpt world -> evalBinary LtEqFns fnName evaledArgs originOpt world
        | ">=" -> fun evaledArgs originOpt world -> evalBinary GtEqFns fnName evaledArgs originOpt world
        | "+" -> fun evaledArgs originOpt world -> evalBinary AddFns fnName evaledArgs originOpt world
        | "-" -> fun evaledArgs originOpt world -> evalBinary SubFns fnName evaledArgs originOpt world
        | "*" -> fun evaledArgs originOpt world -> evalBinary MulFns fnName evaledArgs originOpt world
        | "/" -> fun evaledArgs originOpt world -> evalBinary DivFns fnName evaledArgs originOpt world
        | "%" -> fun evaledArgs originOpt world -> evalBinary ModFns fnName evaledArgs originOpt world
        | "!" -> fun evaledArgs originOpt world -> evalSinglet evalDereference fnName evaledArgs originOpt world
        | "not" -> fun evaledArgs originOpt world -> evalBoolUnary not fnName evaledArgs originOpt world
        | "hash" -> fun evaledArgs originOpt world -> evalUnary HashFns fnName evaledArgs originOpt world
        | "toEmpty" -> fun evaledArgs originOpt world -> evalUnary ToEmptyFns fnName evaledArgs originOpt world
        | "toIdentity" -> fun evaledArgs originOpt world -> evalUnary ToIdentityFns fnName evaledArgs originOpt world
        | "toMin" -> fun evaledArgs originOpt world -> evalUnary ToMinFns fnName evaledArgs originOpt world
        | "toMax" -> fun evaledArgs originOpt world -> evalUnary ToMaxFns fnName evaledArgs originOpt world
        | "inc" -> fun evaledArgs originOpt world -> evalUnary IncFns fnName evaledArgs originOpt world
        | "dec" -> fun evaledArgs originOpt world -> evalUnary DecFns fnName evaledArgs originOpt world
        | "negate" -> fun evaledArgs originOpt world -> evalUnary NegateFns fnName evaledArgs originOpt world
        | "pow" -> fun evaledArgs originOpt world -> evalBinary PowFns fnName evaledArgs originOpt world
        | "root" -> fun evaledArgs originOpt world -> evalBinary RootFns fnName evaledArgs originOpt world
        | "sqr" -> fun evaledArgs originOpt world -> evalUnary SqrFns fnName evaledArgs originOpt world
        | "sqrt" -> fun evaledArgs originOpt world -> evalUnary SqrtFns fnName evaledArgs originOpt world
        | "floor" -> fun evaledArgs originOpt world -> evalUnary FloorFns fnName evaledArgs originOpt world
        | "ceiling" -> fun evaledArgs originOpt world -> evalUnary CeilingFns fnName evaledArgs originOpt world
        | "truncate" -> fun evaledArgs originOpt world -> evalUnary TruncateFns fnName evaledArgs originOpt world
        | "round" -> fun evaledArgs originOpt world -> evalUnary RoundFns fnName evaledArgs originOpt world
        | "exp" -> fun evaledArgs originOpt world -> evalUnary ExpFns fnName evaledArgs originOpt world
        | "log" -> fun evaledArgs originOpt world -> evalUnary LogFns fnName evaledArgs originOpt world
        | "sin" -> fun evaledArgs originOpt world -> evalUnary SinFns fnName evaledArgs originOpt world
        | "cos" -> fun evaledArgs originOpt world -> evalUnary CosFns fnName evaledArgs originOpt world
        | "tan" -> fun evaledArgs originOpt world -> evalUnary TanFns fnName evaledArgs originOpt world
        | "asin" -> fun evaledArgs originOpt world -> evalUnary AsinFns fnName evaledArgs originOpt world
        | "acos" -> fun evaledArgs originOpt world -> evalUnary AcosFns fnName evaledArgs originOpt world
        | "atan" -> fun evaledArgs originOpt world -> evalUnary AtanFns fnName evaledArgs originOpt world
        | "length" -> fun evaledArgs originOpt world -> evalUnary LengthFns fnName evaledArgs originOpt world
        | "normal" -> fun evaledArgs originOpt world -> evalUnary NormalFns fnName evaledArgs originOpt world
        | "cross" -> fun evaledArgs originOpt world -> evalBinary CrossFns fnName evaledArgs originOpt world
        | "dot" -> fun evaledArgs originOpt world -> evalBinary DotFns fnName evaledArgs originOpt world
        | "bool" -> fun evaledArgs originOpt world -> evalUnary BoolFns fnName evaledArgs originOpt world
        | "int" -> fun evaledArgs originOpt world -> evalUnary IntFns fnName evaledArgs originOpt world
        | "int64" -> fun evaledArgs originOpt world -> evalUnary Int64Fns fnName evaledArgs originOpt world
        | "single" -> fun evaledArgs originOpt world -> evalUnary SingleFns fnName evaledArgs originOpt world
        | "double" -> fun evaledArgs originOpt world -> evalUnary DoubleFns fnName evaledArgs originOpt world
        | "string" -> fun evaledArgs originOpt world -> evalUnary StringFns fnName evaledArgs originOpt world
        | "getTypeName" -> fun evaledArgs originOpt world -> evalSinglet evalGetTypeName fnName evaledArgs originOpt world
        | "tryIndex" -> fun evaledArgs originOpt world -> evalDoublet evalTryIndex fnName evaledArgs originOpt world
        | "hasIndex" -> fun evaledArgs originOpt world -> evalDoublet evalHasIndex fnName evaledArgs originOpt world
        | "index" -> fun evaledArgs originOpt world -> evalDoublet evalIndex fnName evaledArgs originOpt world
        | "getName" -> fun evaledArgs originOpt world -> evalSinglet evalGetName fnName evaledArgs originOpt world
        | "tuple" -> fun evaledArgs originOpt world -> evalTuple fnName evaledArgs originOpt world
        | "pair" -> fun evaledArgs originOpt world -> evalTuple fnName evaledArgs originOpt world
        | "fst" -> fun evaledArgs originOpt world -> evalSinglet (evalIndexInt 0) fnName evaledArgs originOpt world
        | "snd" -> fun evaledArgs originOpt world -> evalSinglet (evalIndexInt 1) fnName evaledArgs originOpt world
        | "thd" -> fun evaledArgs originOpt world -> evalSinglet (evalIndexInt 2) fnName evaledArgs originOpt world
        | "fth" -> fun evaledArgs originOpt world -> evalSinglet (evalIndexInt 3) fnName evaledArgs originOpt world
        | "fif" -> fun evaledArgs originOpt world -> evalSinglet (evalIndexInt 4) fnName evaledArgs originOpt world
        | "nth" -> fun evaledArgs originOpt world -> evalDoublet evalNth fnName evaledArgs originOpt world
        | "some" -> fun evaledArgs originOpt world -> evalSinglet evalSome fnName evaledArgs originOpt world
        | "isNone" -> fun evaledArgs originOpt world -> evalSinglet evalIsNone fnName evaledArgs originOpt world
        | "isSome" -> fun evaledArgs originOpt world -> evalSinglet evalIsSome fnName evaledArgs originOpt world
        | "isEmpty" -> fun evaledArgs originOpt world -> evalSinglet (evalIsEmpty evalApply) fnName evaledArgs originOpt world
        | "notEmpty" -> fun evaledArgs originOpt world -> evalSinglet (evalNotEmpty evalApply) fnName evaledArgs originOpt world
        | "tryUncons" -> fun evaledArgs originOpt world -> evalSinglet (evalTryUncons evalApply) fnName evaledArgs originOpt world
        | "uncons" -> fun evaledArgs originOpt world -> evalSinglet (evalUncons evalApply) fnName evaledArgs originOpt world
        | "cons" -> fun evaledArgs originOpt world -> evalDoublet evalCons fnName evaledArgs originOpt world
        | "commit" -> fun evaledArgs originOpt world -> evalSinglet evalCommit fnName evaledArgs originOpt world
        | "tryHead" -> fun evaledArgs originOpt world -> evalSinglet (evalTryHead evalApply) fnName evaledArgs originOpt world
        | "head" -> fun evaledArgs originOpt world -> evalSinglet (evalHead evalApply) fnName evaledArgs originOpt world
        | "tryTail" -> fun evaledArgs originOpt world -> evalSinglet (evalTryTail evalApply) fnName evaledArgs originOpt world
        | "tail" -> fun evaledArgs originOpt world -> evalSinglet (evalTail evalApply) fnName evaledArgs originOpt world
        | "scanWhile" -> fun evaledArgs originOpt world -> evalTriplet (evalScanWhile evalApply) fnName evaledArgs originOpt world
        | "scani" -> fun evaledArgs originOpt world -> evalTriplet (evalScani evalApply) fnName evaledArgs originOpt world
        | "scan" -> fun evaledArgs originOpt world -> evalTriplet (evalScan evalApply) fnName evaledArgs originOpt world
        | "foldWhile" -> fun evaledArgs originOpt world -> evalTriplet (evalFoldWhile evalApply) fnName evaledArgs originOpt world
        | "foldi" -> fun evaledArgs originOpt world -> evalTriplet (evalFoldi evalApply) fnName evaledArgs originOpt world
        | "fold" -> fun evaledArgs originOpt world -> evalTriplet (evalFold evalApply) fnName evaledArgs originOpt world
        | "mapi" -> fun evaledArgs originOpt world -> evalDoublet (evalMapi evalApply) fnName evaledArgs originOpt world
        | "map" -> fun evaledArgs originOpt world -> evalDoublet (evalMap evalApply) fnName evaledArgs originOpt world
        | "contains" -> fun evaledArgs originOpt world -> evalDoublet (evalContains evalApply) fnName evaledArgs originOpt world
        | "toString" -> fun evaledArgs originOpt world -> evalSinglet evalToString fnName evaledArgs originOpt world
        | "codata" -> fun evaledArgs originOpt world -> evalDoublet evalCodata fnName evaledArgs originOpt world
        | "toCodata" -> fun evaledArgs originOpt world -> evalSinglet evalToCodata fnName evaledArgs originOpt world
        | "list" -> fun evaledArgs originOpt world -> evalList fnName evaledArgs originOpt world
        | "toList" -> fun evaledArgs originOpt world -> evalSinglet evalToList fnName evaledArgs originOpt world
        | "ring" -> fun evaledArgs originOpt world -> evalRing fnName evaledArgs originOpt world
        | "toRing" -> fun evaledArgs originOpt world -> evalSinglet evalToRing fnName evaledArgs originOpt world
        | "add" -> fun evaledArgs originOpt world -> evalDoublet evalCons fnName evaledArgs originOpt world
        | "remove" -> fun evaledArgs originOpt world -> evalDoublet evalRemove fnName evaledArgs originOpt world
        | "toTable" -> fun evaledArgs originOpt world -> evalSinglet evalToTable fnName evaledArgs originOpt world
        | _ -> getOverload fnName

    and evalUnionUnevaled name exprs world =
        let struct (evaleds, world) = evalMany exprs world
        struct (Union (name, evaleds), world)

    and evalTableUnevaled exprPairs world =
        let struct (evaledPairs, world) =
            List.fold (fun struct (evaledPairs, world) (exprKey, exprValue) ->
                let struct (evaledKey, world) = eval exprKey world
                let struct (evaledValue, world) = eval exprValue world
                struct ((evaledKey, evaledValue) :: evaledPairs, world))
                struct ([], world)
                exprPairs
        let evaledPairs = List.rev evaledPairs
        struct (Table (Map.ofList evaledPairs), world)

    and evalRecordUnevaled name exprPairs world =
        let struct (evaledPairs, world) =
            List.fold (fun struct (evaledPairs, world) (fieldName, expr) ->
                let struct (evaledValue, world) = eval expr world
                struct ((fieldName, evaledValue) :: evaledPairs, world))
                struct ([], world)
                exprPairs
        let evaledPairs = List.rev evaledPairs
        let map = evaledPairs |> List.mapi (fun i (fieldName, _) -> (fieldName, i)) |> Map.ofList
        let fields = evaledPairs |> List.map snd |> Array.ofList
        struct (Record (name, map, fields), world)

    and evalBinding expr name cachedBinding originOpt world =
        match tryGetBinding name cachedBinding world with
        | None ->
            if world.IsExtrinsic name then struct (expr, world)
            elif isIntrinsic name then struct (expr, world)
            else struct (Violation (["NonexistentBinding"], "Non-existent binding '" + name + "'.", originOpt), world)
        | Some binding -> struct (binding, world)

    and evalUpdateIntInner fnName index target value originOpt world =
        match target with
        | String str ->
            if index >= 0 && index < String.length str then
                match value with
                | String str2 when str2.Length = 1 ->
                    let left = str.Substring (0, index)
                    let right = str.Substring (index, str.Length)
                    Right struct (String (left + str2 + right), world)
                | _ -> Left struct (Violation (["InvalidArgumentValue"; String.capitalize fnName], "String update value must be a String of length 1.", originOpt), world)
            else Left struct (Violation (["OutOfRangeArgument"; String.capitalize fnName], "String does not contain element at index " + string index + ".", originOpt), world)
        | Option opt ->
            match (index, opt) with
            | (0, Some value) -> Right struct (value, world)
            | (_, _) -> Left struct (Violation (["OutOfRangeArgument"; String.capitalize fnName], "Could not update at index " + string index + ".", originOpt), world)
        | List _ -> Left struct (Violation (["NotImplemented"; String.capitalize fnName], "Updating lists by index is not yet implemented.", originOpt), world) // TODO: implement
        | Table map -> Right struct (Table (Map.add (Int index) value map), world)
        | Tuple elements
        | Union (_, elements)
        | Record (_, _, elements) ->
            if index < elements.Length then
                let elements' = Array.copy elements
                elements'.[index] <- value
                match target with
                | Tuple _ -> Right struct (Tuple elements', world)
                | Union (name, _) -> Right struct (Union (name, elements'), world)
                | Record (name, map, _) -> Right struct (Record (name, map, elements'), world)
                | _ -> failwithumf ()
            else Left struct (Violation (["OutOfRangeArgument"; String.capitalize fnName], "Could not update structure at index " + string index + ".", originOpt), world)
        | _ ->
            match evalOverload fnName [|Int index; value; target|] originOpt world with
            | struct (Violation _, _) as error -> Left error
            | struct (_, _) as success -> Right success

    and evalUpdateKeywordInner fnName keyword target value originOpt world =
        match target with
        | Table map ->
            Right struct (Table (Map.add (Keyword keyword) value map), world)
        | Record (name, map, fields) ->
            match Map.tryFind keyword map with
            | Some index ->
                if index < fields.Length then
                    let fields' = Array.copy fields
                    fields'.[index] <- value
                    Right struct (Record (name, map, fields'), world)
                else Left struct (Violation (["OutOfRangeArgument"; String.capitalize fnName], "Record does not contain element with name '" + name + "'.", originOpt), world)
            | None ->
                Left struct (Violation (["OutOfRangeArgument"; String.capitalize fnName], "Record does not contain element with name '" + name + "'.", originOpt), world)
        | Violation _ as violation ->
            Left struct (violation, world)
        | _ ->
            match evalOverload fnName [|Keyword keyword; value; target|] originOpt world with
            | struct (Violation _, _) as error -> Left error
            | struct (_, _) as success -> Right success

    and evalUpdateInner fnName indexerExpr targetExpr valueExpr originOpt world =
        let struct (indexer, world) = eval indexerExpr world
        let struct (target, world) = eval targetExpr world
        let struct (value, world) = eval valueExpr world
        match indexer with
        | Violation _ as v -> Left struct (v, world)
        | Int index -> evalUpdateIntInner fnName index target value originOpt world
        | Keyword keyword -> evalUpdateKeywordInner fnName keyword target value originOpt world
        | _ ->
            match target with
            | Table map -> Right struct (Table (Map.add indexer valueExpr map), world)
            | _ ->
                match evalOverload fnName [|indexer; value; target|] originOpt world with
                | struct (Violation _, _) as error -> Left error
                | struct (_, _) as success -> Right success

    and evalTryUpdate fnName indexerExpr targetExpr valueExpr originOpt world =
        match evalUpdateInner fnName indexerExpr targetExpr valueExpr originOpt world with
        | Right struct (evaled, world) -> struct (Option (Some evaled), world)
        | Left struct (_, world) -> struct (Option None, world)

    and evalUpdate fnName indexerExpr targetExpr valueExpr originOpt world =
        match evalUpdateInner fnName indexerExpr targetExpr valueExpr originOpt world with
        | Right success -> success
        | Left error -> error

    and evalApply exprs originOpt (world : 'w) =
        if Array.notEmpty exprs then
            let (exprsHead, exprsTail) = (Array.head exprs, Array.tail exprs)
            let struct (evaledHead, world) = eval exprsHead world in annotateWorld world // force the type checker to see the world as it is
            match evaledHead with
            | Keyword keyword ->
                let struct (evaledTail, world) = evalMany exprsTail world
                let union = Union (keyword, evaledTail)
                struct (union, world)
            | Binding (fnName, cachedBinding, originOpt) ->
                // NOTE: when evaluation leads here, we can (actually must) infer that we have
                // either an extrinsic or intrinsic function.
                let directBinding =
                    match !cachedBinding with
                    | UncachedBinding ->
                        if world.IsExtrinsic fnName then
                            let directBinding = world.GetExtrinsic fnName
                            let directBinding = fun exprsTail originOpt (worldObj : obj) ->
                                let struct (result, world) = directBinding exprsTail originOpt (worldObj :?> 'w)
                                struct (result, box world)
                            cachedBinding := DirectBinding directBinding
                            directBinding
                        else
                            let directBinding = getIntrinsic fnName
                            let directBinding = fun exprsTail originOpt (worldObj : obj) ->
                                let struct (evaledTail, world) = evalMany exprsTail (worldObj :?> 'w)
                                let struct (result, world) = directBinding evaledTail originOpt world
                                struct (result, box world)
                            cachedBinding := DirectBinding directBinding
                            directBinding
                    | DeclarationBinding _ | ProceduralBinding _ -> failwithumf ()
                    | DirectBinding directBinding -> directBinding
                let struct (result, worldObj) = directBinding exprsTail originOpt world
                struct (result, worldObj :?> 'w)
            | Fun (pars, parsCount, body, _, framesOpt, originOpt) ->
                let struct (evaledTail, world) = evalMany exprsTail world
                let struct (framesCurrentOpt, world) =
                    match framesOpt with
                    | Some frames ->
                        let framesCurrent = getProceduralFrames world
                        let world = setProceduralFrames (frames :?> ProceduralFrame list) world
                        struct (Some framesCurrent, world)
                    | None -> struct (None, world)
                let struct (evaled, world) =
                    if evaledTail.Length = parsCount then
                        let bindings = Array.map2 (fun par evaledArg -> struct (par, evaledArg)) pars evaledTail
                        let world = addProceduralBindings (AddToNewFrame parsCount) bindings world
                        let struct (evaled, world) = eval body world
                        struct (evaled, removeProceduralBindings world)
                    else struct (Violation (["MalformedLambdaInvocation"], "Wrong number of arguments.", originOpt), world)
                match framesCurrentOpt with
                | Some framesCurrent ->
                    let world = setProceduralFrames framesCurrent world
                    struct (evaled, world)
                | None -> struct (evaled, world)
            | Violation _ as error -> struct (error, world)
            | _ -> struct (Violation (["MalformedApplication"], "Cannot apply the non-binding '" + scstring evaledHead + "'.", originOpt), world)
        else struct (Unit, world)

    and evalApplyAnd exprs originOpt world =
        match exprs with
        | [|left; right|] ->
            match eval left world with
            | struct (Bool false, _) as never -> never
            | struct (Bool true, world) ->
                match eval right world with
                | struct (Bool _, _) as result -> result
                | struct (Violation _, _) as error -> error
                | _ -> struct (Violation (["InvalidArgumentType"; "&&"], "Cannot apply a logic function to non-Bool values.", originOpt), world)
            | struct (Violation _, _) as error -> error
            | _ -> struct (Violation (["InvalidArgumentType"; "&&"], "Cannot apply a logic function to non-Bool values.", originOpt), world)
        | _ -> struct (Violation (["InvalidArgumentCount"; "&&"], "Incorrect number of arguments for application of '&&'; 2 arguments required.", originOpt), world)

    and evalApplyOr exprs originOpt world =
        match exprs with
        | [|left; right|] ->
            match eval left world with
            | struct (Bool true, _) as always -> always
            | struct (Bool false, world) ->
                match eval right world with
                | struct (Bool _, _) as result -> result
                | struct (Violation _, _) as error -> error
                | _ -> struct (Violation (["InvalidArgumentType"; "&&"], "Cannot apply a logic function to non-Bool values.", originOpt), world)
            | struct (Violation _, _) as error -> error
            | _ -> struct (Violation (["InvalidArgumentType"; "&&"], "Cannot apply a logic function to non-Bool values.", originOpt), world)
        | _ -> struct (Violation (["InvalidArgumentCount"; "&&"], "Incorrect number of arguments for application of '&&'; 2 arguments required.", originOpt), world)

    and evalLet4 binding body originOpt world =
        let world =
            match binding with
            | VariableBinding (name, body) ->
                let struct (evaled, world) = eval body world
                addProceduralBinding (AddToNewFrame 1) name evaled world
            | FunctionBinding (name, args, body) ->
                let frames = getProceduralFrames world :> obj
                let fn = Fun (args, args.Length, body, true, Some frames, originOpt)
                addProceduralBinding (AddToNewFrame 1) name fn world
        let struct (evaled, world) = eval body world
        struct (evaled, removeProceduralBindings world)

    and evalLetMany4 bindingsHead bindingsTail bindingsCount body originOpt world =
        let world =
            match bindingsHead with
            | VariableBinding (name, body) ->
                let struct (bodyValue, world) = eval body world
                addProceduralBinding (AddToNewFrame bindingsCount) name bodyValue world
            | FunctionBinding (name, args, body) ->
                let frames = getProceduralFrames world :> obj
                let fn = Fun (args, args.Length, body, true, Some frames, originOpt)
                addProceduralBinding (AddToNewFrame bindingsCount) name fn world
        let world =
            List.foldi (fun i world binding ->
                match binding with
                | VariableBinding (name, body) ->
                    let struct (bodyValue, world) = eval body world
                    addProceduralBinding (AddToHeadFrame ^ inc i) name bodyValue world
                | FunctionBinding (name, args, body) ->
                    let frames = getProceduralFrames world :> obj
                    let fn = Fun (args, args.Length, body, true, Some frames, originOpt)
                    addProceduralBinding (AddToHeadFrame ^ inc i) name fn world)
                world
                bindingsTail
        let struct (evaled, world) = eval body world
        struct (evaled, removeProceduralBindings world)
        
    and evalLet binding body originOpt world =
        evalLet4 binding body originOpt world
        
    and evalLetMany bindings body originOpt world =
        match bindings with
        | bindingsHead :: bindingsTail ->
            let bindingsCount = List.length bindingsTail + 1
            evalLetMany4 bindingsHead bindingsTail bindingsCount body originOpt world
        | [] -> struct (Violation (["MalformedLetOperation"], "Let operation must have at least 1 binding.", originOpt), world)

    and evalFun fn pars parsCount body framesPushed framesOpt originOpt world =
        if not framesPushed then
            if Option.isNone framesOpt then
                let frames = getProceduralFrames world :> obj
                struct (Fun (pars, parsCount, body, true, Some frames, originOpt), world)
            else struct (Fun (pars, parsCount, body, true, framesOpt, originOpt), world)
        else struct (fn, world)

    and evalIf condition consequent alternative originOpt world =
        match eval condition world with
        | struct (Bool bool, world) -> if bool then eval consequent world else eval alternative world
        | struct (Violation _ as evaled, world) -> struct (evaled, world)
        | struct (_, world) -> struct (Violation (["InvalidIfCondition"], "Must provide an expression that evaluates to a Bool in an if condition.", originOpt), world)

    and evalMatch input (cases : (Expr * Expr) array) originOpt world =
        let struct (input, world) = eval input world
        let resultEir =
            Seq.foldUntilRight (fun world (condition, consequent) ->
                let struct (evaledInput, world) = eval condition world
                match evalBinaryInner EqFns "=" input evaledInput originOpt world with
                | struct (Bool true, world) -> Right (eval consequent world)
                | struct (Bool false, world) -> Left world
                | struct (Violation _, world) -> Right struct (evaledInput, world)
                | _ -> failwithumf ())
                (Left world)
                cases
        match resultEir with
        | Right success -> success
        | Left world -> struct (Violation (["InexhaustiveMatch"], "A match expression failed to satisfy any of its cases.", originOpt), world)

    and evalSelect exprPairs originOpt world =
        let resultEir =
            Seq.foldUntilRight (fun world (condition, consequent) ->
                match eval condition world with
                | struct (Bool bool, world) -> if bool then Right (eval consequent world) else Left world
                | struct (Violation _ as evaled, world) -> Right struct (evaled, world)
                | struct (_, world) -> Right struct (Violation (["InvalidSelectCondition"], "Must provide an expression that evaluates to a Bool in a case condition.", originOpt), world))
                (Left world)
                exprPairs
        match resultEir with
        | Right success -> success
        | Left world -> struct (Violation (["InexhaustiveSelect"], "A select expression failed to satisfy any of its cases.", originOpt), world)

    and evalTry body handlers _ world =
        match eval body world with
        | struct (Violation (categories, _, _) as evaled, world) ->
            match
                List.foldUntilRight (fun world (handlerCategories, handlerBody) ->
                    let categoriesTrunc = List.truncate (List.length handlerCategories) categories
                    if categoriesTrunc = handlerCategories then Right (eval handlerBody world) else Left world)
                    (Left world)
                    handlers with
            | Right success -> success
            | Left world -> struct (evaled, world)
        | success -> success

    and evalDo exprs _ world =
        let evaledEir =
            List.foldWhileRight (fun struct (_, world) expr ->
                match eval expr world with
                | struct (Violation _, _) as error -> Left error
                | success -> Right success)
                (Right struct (Unit, world))
                exprs
        Either.amb evaledEir

    and evalDefine binding originOpt world =
        let struct (bound, world) =
            match binding with
            | VariableBinding (name, body) ->
                let struct (evaled, world) = eval body world
                tryAddDeclarationBinding name evaled world
            | FunctionBinding (name, args, body) ->
                let frames = getProceduralFrames world :> obj
                let fn = Fun (args, args.Length, body, true, Some frames, originOpt)
                tryAddDeclarationBinding name fn world
        if bound
        then struct (Unit, world)
        else struct (Violation (["InvalidDeclaration"], "Can make declarations only at the top-level.", None), world)

    /// Evaluate an expression.
    and eval expr world =
        match expr with
        | Violation _
        | Unit _
        | Bool _
        | Int _
        | Int64 _
        | Single _
        | Double _
        | String _
        | Keyword _
        | Tuple _
        | Union _
        | Pluggable _
        | Option _
        | Codata _
        | List _
        | Ring _
        | Table _
        | Record _ -> struct (expr, world)
        | UnionUnevaled (name, exprs) -> evalUnionUnevaled name exprs world
        | TableUnevaled exprPairs -> evalTableUnevaled exprPairs world
        | RecordUnevaled (name, exprPairs) -> evalRecordUnevaled name exprPairs world
        | Binding (name, cachedBinding, originOpt) as expr -> evalBinding expr name cachedBinding originOpt world
        | TryUpdate (expr, expr2, expr3, _, originOpt) -> evalTryUpdate "tryUpdate" expr expr2 expr3 originOpt world
        | Update (expr, expr2, expr3, _, originOpt) -> evalUpdate "update" expr expr2 expr3 originOpt world
        | Apply (exprs, _, originOpt) -> evalApply exprs originOpt world
        | ApplyAnd (exprs, _, originOpt) -> evalApplyAnd exprs originOpt world
        | ApplyOr (exprs, _, originOpt) -> evalApplyOr exprs originOpt world
        | Let (binding, body, originOpt) -> evalLet binding body originOpt world
        | LetMany (bindings, body, originOpt) -> evalLetMany bindings body originOpt world
        | Fun (pars, parsCount, body, framesPushed, framesOpt, originOpt) as fn -> evalFun fn pars parsCount body framesPushed framesOpt originOpt world
        | If (condition, consequent, alternative, originOpt) -> evalIf condition consequent alternative originOpt world
        | Match (input, cases, originOpt) -> evalMatch input cases originOpt world
        | Select (exprPairs, originOpt) -> evalSelect exprPairs originOpt world
        | Try (body, handlers, originOpt) -> evalTry body handlers originOpt world
        | Do (exprs, originOpt) -> evalDo exprs originOpt world
        | Quote _ as quote -> struct (quote, world)
        | Define (binding, originOpt) -> evalDefine binding originOpt world

    /// Evaluate a sequence of expressions.
    and evalMany (exprs : Expr array) world =
        let evaleds = Array.zeroCreate exprs.Length
        let world =
            Seq.foldi
                (fun i world expr ->
                    let struct (evaled, world) = eval expr world
                    evaleds.[i] <- evaled
                    world)
                world
                exprs
        struct (evaleds, world)

    /// Evaluate an expression, with logging on violation result.
    let evalWithLogging expr world =
        let struct (evaled, world) = eval expr world
        log evaled
        struct (evaled, world)

    /// Evaluate a series of expressions, with logging on violation result.
    let evalManyWithLogging exprs world =
        let struct (evaleds, world) = evalMany exprs world
        Array.iter log evaleds
        struct (evaleds, world)

    /// Attempt to evaluate a script.
    let tryEvalScript choose scriptFilePath world =
        Log.info ("Evaluating script '" + scriptFilePath + "...")
        try let scriptStr =
                scriptFilePath |>
                File.ReadAllText |>
                String.unescape
            let script =
                scriptStr |>
                (fun str -> Symbol.OpenSymbolsStr + str + Symbol.CloseSymbolsStr) |>
                scvalue<Expr array>
            let struct (evaleds, world) = evalMany script world
            Log.info ("Successfully evaluated script '" + scriptFilePath + ".")
            Right struct (scriptStr, evaleds, world)
        with exn ->
            let error = "Failed to evaluate script '" + scriptFilePath + "' due to: " + scstring exn
            Log.info error
            Left struct (error, choose world)