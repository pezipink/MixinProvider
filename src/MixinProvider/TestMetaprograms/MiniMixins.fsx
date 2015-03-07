#I @"..\..\..\packages\FSharp.Compiler.Service\lib\net45\"
#r "FSharp.Compiler.Service"
#if MIXIN 

#else
#I @"..\..\..\bin\"
#endif

#r "MixinProvider"

open System
open System.IO
open System.Text
open MixinProvider
open Microsoft.FSharp.Compiler.Interactive
open Microsoft.FSharp.Compiler.Interactive.Shell

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Compiler.SourceCodeServices

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
            )
    // now we have the info required to make baby mixin type providers!
    let ctypeprovider name args  =
         api (sprintf "type %s_mixin" name) >> newli
         >> ap (sprintf "type %sProvider" name) >> newl

    ()
    
        

