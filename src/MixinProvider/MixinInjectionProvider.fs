namespace MixinProvider

open Microsoft.FSharp.Core.CompilerServices
open System
open System.Collections.Generic
open System.Reflection
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Quotations
open MixinProvider
open MixinCompiler

type mixin_inject() = inherit obj()

// dummy inject functions and attributes
[<AutoOpen>]
module Injection =
    let mixin_inject injected = ()
    let mixin_inject1 a injected = ()
    let mixin_inject2 a b injected = ()
    let mixin_inject3 a b c injected = ()
    let mixin_inject4 a b c d injected = ()
    let mixin_inject5 a b c d e injected = ()

    type mixin_injectAttribute(injected) = inherit Attribute()
    type mixin_inject1Attribute(a,injected) = inherit Attribute()
    type mixin_inject2Attribute(a,b,injected) = inherit Attribute()
    type mixin_inject3Attribute(a,b,c,injected) = inherit Attribute()
    type mixin_inject4Attribute(a,b,c,d,injected) = inherit Attribute()
    type mixin_inject5Attribute(a,b,c,d,e,injected) = inherit Attribute()

type ProjectSeekMode =
    | Absolute = 1
    | Scan = 2
    | RecursiveScan = 3

[<TypeProvider>]
type MixinInjectionProvider() =
    inherit MixinProvider()
    let checkProject projectFile = 
        let checker = FSharpChecker.Create(keepAssemblyContents=true)
        let projOptions = checker.GetProjectOptionsFromProjectFile projectFile 
        let results = checker.ParseAndCheckProject projOptions |> Async.RunSynchronously

        let rec declarations d = seq {
            match d with 
            | FSharpImplementationFileDeclaration.Entity(e,sub) as dec ->       
                yield dec
                yield! Seq.map declarations sub |> Seq.collect id
            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) as dec -> 
                yield dec
            | FSharpImplementationFileDeclaration.InitAction(_)as dec ->
                yield dec
            }

        let injectionNames =
            ["mixin_inject1"
             "mixin_inject2"
             "mixin_inject3"
             "mixin_inject4"
             "mixin_inject5"
             "mixin_inject1Attribute"
             "mixin_inject2Attribute"
             "mixin_inject3Attribute"
             "mixin_inject4Attribute"
             "mixin_inject5Attribute"]

        results.AssemblyContents.ImplementationFiles.[0].Declarations
        |> Seq.map declarations
        |> Seq.collect id
        |> Seq.tryPick(
            function 
            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) 
                when List.exists((=)v.DisplayName) injectionNames ->
                Some (v.DisplayName, vs |> List.collect id |> List.map (fun e -> e.DisplayName,e.FullType))
            | _ -> None)
        |> function
            | None -> None
            | Some types -> Some(projectFile, types)


    let findFsprojects dir =
        Directory.GetFiles dir
        |> Array.filter(fun fi -> fi.EndsWith ".fsproj")
        |> Array.toList

    let projectScan mode param = 
        match mode with
        | ProjectSeekMode.Absolute ->
            if File.Exists param = false then failwith "could not locate specified F# project file"
            else [param]
        | ProjectSeekMode.Scan -> 
            if Directory.Exists param = false then failwith "the specified directory could not be found"
            else
            match findFsprojects param with
            | [] -> failwith "could not find any F# projects in the specifed directory"
            | xs -> xs
        | ProjectSeekMode.RecursiveScan -> 
            if Directory.Exists param = false then failwith "the specified directory could not be found"
            else
            let rec directories current = seq {
                yield current
                for next in Directory.GetDirectories current do
                    yield! directories current}
            directories param
            |> Seq.toList
            |> List.map findFsprojects
            |> List.collect id
            |> function
                | [] -> failwith "could not find any F# projects in the specifed directories"
                | xs -> xs
        | _ -> failwith "impossible"

    let injectMetaprogram env =
        // 1. evaluate metaprogram via FSI
        // 2. use the fschecker to locate points of injection
        // 3. route identified dummy fuctions and their params
        //      to the equivalent in the metaprogram, and inject the 
        //      resulting code to that location in the file 
        // 4. what could possibly go wrong
        EvaluationSuccessful("let result = \"injection process completed succdessfully\"")


    override this.AvailableTypes() = [| typeof<mixin_inject> |]

    override this.ResolveType(_) = typeof<mixin_inject>

    override this.StaticParameters() = 
        [|
            helpers.stringParameter "injectionMetaprogram" None  
            helpers.genericOptionalParameter "projectSeekMode" ProjectSeekMode.Scan
            helpers.stringParameter "projectSeekPath" None
        |]

    override this.ApplyStaticArgs(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
        let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
        let metaprogram = helpers.resolveMetaprogram(staticArguments.[0] :?> string)
        let seekMode = staticArguments.[0] :?> ProjectSeekMode
        let seekPath = staticArguments.[0] :?> string
        let wrapperType = WrapperType.Module moduleName
        this.ExecuteMixinCompile(
            typeWithoutArguments, 
            typePathWithArguments, 
            metaprogram, 
            wrapperType, 
            CompileMode.AlwaysCompile, 
            "", 
            "",
            injectMetaprogram,
            MixinCompiler.fscCompile)


[<assembly:TypeProviderAssembly>]
do  
    
    ()