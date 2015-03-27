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
#I @"F:\GIT\FSharp.Compiler.Service\bin\v4.5\"
#r "FSharp.Compiler.Service" 
#I @"..\..\..\bin\"
#r @"MixinProvider.dll"
#endif

//#load @"F:\dropbox\FsEye\FsEye.fsx"

open System
open System.IO
open System.Text
open MixinProvider
open Microsoft.FSharp.Compiler.Interactive
open Microsoft.FSharp.Compiler.Interactive.Shell

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Compiler.SourceCodeServices


type recordTypeMeta = { 
    originalType: string
    newType: string
    properties : (string * string) list }
    
 
let getData source =
    let checker = FSharpChecker.Create(keepAssemblyContents=true)
    let projOptions = checker.GetProjectOptionsFromProjectFile source
    let results =  checker.ParseAndCheckProject projOptions |> Async.RunSynchronously
    let isSqlType (t:FSharpType) = 
    
        if t.HasTypeDefinition && t.TypeDefinition.LogicalName.StartsWith("SqlDataProvider,") then true
        else
        match t.BaseType with
        | Some(value) when value.TypeDefinition.CompiledName = "SqlEntity" -> true
        | _ -> false

    let rec getSqlEntities aux (t:FSharpType) =
        if isSqlType t then t :: aux
        else
        if not t.IsGenericParameter then
            let y = Seq.map(getSqlEntities []) t.GenericArguments |> Seq.toList |> List.collect id
            y @ aux
        else aux

    let toRecordTypeMeta (typeAlias) (t:FSharpType) =
        let name = sprintf "%s.dataContext.``%s``" typeAlias (t.TypeDefinition.CompiledName)
        let props = 
            t.TypeDefinition.MembersFunctionsAndValues
            |> Seq.map(fun p -> p.CompiledName, p.FullType.ToString().Substring 5)
            |> Seq.filter(fun (_,t) -> not <| t.Contains("IQueryable"))
            |> Seq.toList
        { originalType = name; newType = t.TypeDefinition.CompiledName.Replace("[","").Replace("]","").Replace(".",""); properties = props }

    let x =
        results.GetAllUsesOfAllSymbols() 
        |> Async.RunSynchronously
        |> Seq.fold(fun (entities,types) s -> 
            match s.Symbol with 
            | :? FSharpMemberOrFunctionOrValue as e -> 
                (entities,(getSqlEntities types e.FullType))
            | :? FSharpEntity as e ->
                if e.IsFSharpAbbreviation && e.AbbreviatedType.TypeDefinition.IsProvidedAndErased then (e::entities,types)
                else (entities,types)
            | _ -> (entities,types) ) ([],[])
        |> (fun (entities, types) -> 
            entities |> Seq.distinctBy(fun x -> x.LogicalName) |> Seq.toList,   
            types |> Seq.distinctBy(fun x -> x.TypeDefinition.LogicalName) |> Seq.toList)

    let typeName = (fst x).Head.LogicalName

    (snd x) |> List.map (toRecordTypeMeta typeName)


let mixin_inject1 location =
    let genData = getData location
    (0,System.Text.StringBuilder())
    ||> ccode( seq {
      yield! genData |> List.map(fun c -> crecord c.newType c.properties [] )  
      yield! genData |> List.map(fun c -> 
        cextension c.originalType 
            [            
                yield cmember (Instance "this") "" "toRecord" NoArgs 
                    (api (wrapBraces 
                            (System.String.Join("; ", List.map(fun (prop,_) -> sprintf "%s = this.%s" prop prop) c.properties))))
            ]
      )
    })
    |> fun sb -> sb.ToString()
    