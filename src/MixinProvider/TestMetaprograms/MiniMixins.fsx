// at compile time the file will be executing from 
// within \bin\TestMetaprograms, so we have different
// relative assembly paths 
#if MIXIN 
#I @"..\..\packages\FSharp.Compiler.Service\lib\net45\"
#r "FSharp.Compiler.Service" 
#I @"..\"
#r @"MixinProvider.dll"
#else
// these are the design-time relative paths
#I @"..\..\..\packages\FSharp.Compiler.Service\lib\net45\"
#r "FSharp.Compiler.Service" 
#I @"..\..\..\bin\"
#r @"MixinProvider.dll"
#endif

open System
open System.IO
open System.Text
open MixinProvider
open Microsoft.FSharp.Compiler.Interactive
open Microsoft.FSharp.Compiler.Interactive.Shell

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Compiler.SourceCodeServices

// create a mini-mixin type provider consiting of a static parameter for each 
// argument that the corresponding metaprogram's generate function accepts
let ctypeprovider (fileName:string, args)  =
    let name = fileName.Replace(".fsx","").Substring(fileName.LastIndexOf("\\")+1)
    let tname = sprintf "%s_mixin" name
    // ignore any Unit args
    let args = args  |> List.filter(fun (_,ft:FSharpType) -> 
        if ft.ToString() = "type Microsoft.FSharp.Core.unit" then false
        else true )

    // create static parameters
    let paramCreation = seq {
        // use the helper funcions to create a paramete defintions for each argument
        yield! args |> Seq.map(
            fun (name, ftype) -> 
                match ftype.AbbreviatedType.TypeDefinition.QualifiedName with
                | "System.Int32" ->  api (sprintf "helpers.intParameter \"%s\" None" name)
                | "System.String" -> api (sprintf "helpers.stringParameter \"%s\" None" name)
                | t -> failwithf "type %s is not supported in a mini-mixin" t) 

        // these three go on all mini-mixins
        yield api ("helpers.genericOptionalParameter \"compileMode\" CompileMode.CompileWhenMissisng")
        yield api ("helpers.genericOptionalParameter \"generationMode\" GenerationMode.AutoOpenModule")
        yield api ("helpers.stringParameter \"outputLocation\" (Some \"\")") } 
    
    
    // let-bind each expected static parameter (used in ApplyStaticArgs(..))
    let paramExtraction = seq {
        // expect the generated arguments to be first
        yield! args |> Seq.mapi(
            fun i (name, ftype) -> 
                match ftype.AbbreviatedType.TypeDefinition.QualifiedName with
                | "System.Int32" -> clet name (ap (sprintf "staticArguments.[%i] :?> int" i))
                | "System.String" -> clet name (ap (sprintf "staticArguments.[%i] :?> string" i))
                | t -> failwithf "type %s is not supported in a mini-mixin" t)

        // and these three are offset from the amount of generated arguments
        yield clet "compileMode"    (ap (sprintf "staticArguments.[%i] :?> CompileMode"     args.Length))
        yield clet "generationMode" (ap (sprintf "staticArguments.[%i] :?> GenerationMode" (args.Length+1)))
        yield clet "outputLoc   "   (ap (sprintf "staticArguments.[%i] :?> string"         (args.Length+2))) } 

    // define the arguments that are passed to the metaprogram as a string
    // here we simply join the arguments together using String.Join " "
    let mprams =
        match args with
        | [] -> clet "mparams" (ap "\"\"") // no args, blank string
        | args -> clet "mparams" 
                    (ap (sprintf "String.Join(\" \", [|%s|])" 
                        (String.Join("; ", args |> List.map(fun (n,_) -> n))))) // yes I know this looks like LISP
    
    // a type provider compositite code block consists of:      
    ccode [
        // a dummy type that is the name you use as the type provider
        ctype tname [] "obj()" []
        // now the type provider itself
        api ("[<TypeProvider>]")
        ctype (sprintf "%sProvider" name) [] "MixinProvider()"
            [
                cmember (InstanceOverride "this") "" "AvailableTypes" NoArgs 
                    (api (sprintf "[| typeof<%s> |]" tname))
                cmember (InstanceOverride "this") "" "ResolveType" (Tuple ["_",None])  
                    (api (sprintf "typeof<%s>" tname))
                cmember (InstanceOverride "this") "" "StaticParameters" NoArgs 
                    (carray paramCreation)
                cmember (InstanceOverride "this") "" "ApplyStaticArgs" 
                    (Tuple ["typeWithoutArguments", Some "Type"; 
                            "typePathWithArguments", Some "string []";
                            "staticArguments", Some"obj []";]) 
                    (ccode <| seq {
                        yield! paramExtraction
                        yield clet "moduleName" (ap "typePathWithArguments.[typePathWithArguments.Length-1]")
                        yield mprams
                        yield api (sprintf "this.ExecuteMixinCompile(typeWithoutArguments, typePathWithArguments, \"%s\", moduleName, compileMode, outputLoc, mparams, generationMode)" fileName)})
                cmember (InstanceOverride "this") "" "GetNamespace" NoArgs (api "\"MiniMixins\"")
            ]
    ]

let generate metaprogramPath =
    // find all .fsx files in the directory (even this one!)
    if Directory.Exists metaprogramPath |> not then 
        failwithf "could not find metaprogram directory %s " metaprogramPath 

    let metaprograms = Directory.GetFiles metaprogramPath
                        |> Array.filter(fun f -> f.EndsWith ".fsx")

    if metaprograms.Length = 0 then 
        failwithf "could not find any .fsx files in the metaprogram directory %s" metaprogramPath
    
    let metaprogramInfo =
        metaprograms
        |> Array.choose(fun mp -> 
            let checker = FSharpChecker.Create(keepAssemblyContents=true)
            let projOptions =
                printfn "%s"mp
                checker.GetProjectOptionsFromScript(mp, File.ReadAllText mp, otherFlags=[|"--define:MIXIN"|])
                |> Async.RunSynchronously
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

            results.AssemblyContents.ImplementationFiles.[0].Declarations
            |> Seq.map declarations
            |> Seq.collect id
            |> Seq.tryPick(
                function 
                | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) when v.DisplayName = "generate" ->
                    Some (vs |> List.collect id |> List.map (fun e -> e.DisplayName, e.FullType))
                | _ -> None)
            |> function
                | None -> None
                | Some types -> Some(mp, types))
            
    // now we have the info required to make baby mixin type providers!
    let typeProviders = metaprogramInfo |> Array.map ctypeprovider |> Array.toList

    (1,StringBuilder())
    ||> ccode ( seq {        
        yield  newli
        yield  copen ["Microsoft.FSharp.Core.CompilerServices";"MixinProvider";"MixinCompiler";"System"]
        yield  genReferences [] ["MixinProvider.dll"]
        yield! typeProviders
        yield fun i -> ap "[<assembly:TypeProviderAssembly>] do ()"} )
    |> function sb -> sb.ToString()
