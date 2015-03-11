module MixinCompiler

open System.IO
open Microsoft.FSharp.Compiler.Interactive.Shell
open System.Text
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System
open System.Collections.Generic
open System.Reflection

type ResponseMessage =
    | Good of Assembly
    | Bad of Exception

type MixinData = 
    { assemblyLocation : string
      metaprogramParams : string
      assembly : Assembly }
 
type ConfigurationMode =
    /// In debug mode the mixin compiler will output
    /// the generated DLLs with no optimisations, 
    /// the .fs used to compile and the debug symbols for it
    | Debug = 0
    /// Release mode generates an optmised assembly with 
    /// No debug symbols
    | Realese = 1

// this is an enum so it can be used as a static param to the tp
type CompileMode =
    /// Compile when missing will generate an assembly
    /// only if one does not already exist in the expected
    /// location
    | CompileWhenMissisng = 0   
    /// AlwaysCompile will force assemblies to be re-compiled 
    /// every time the type provider fires - be warned this can 
    /// have a large overhead due to the background compiler.
    /// This option is reccomended as a temporary switch if you
    /// wish to force recompilation for some reason
    | AlwaysCompile = 1
    
type GenerationMode =
    /// Default, would be used for most mixins
    | AutoOpenModule = 0
    /// Use this if you don't want to pollute your namespace
    | Module = 1
    /// Namespace is for generating type providers or if you 
    /// Just want types in a namespace.
    | Namespace = 2

type SourceType =
    | File of string
    | Text of string

let isMono = Type.GetType("Mono.Runtime") <> null
    
type MixinCompiler() =
    let (++) path1 path2 = Path.Combine(path1, path2)
    // In Mono all the files we need are actually in one place
    // but in .NET the FSharp.Core.dll is in another castle
    let frameworkReferenceLocation,fsharpCoreLocation =
        if isMono then
            let frameworkReferenceLocation = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
            let fsharpCoreLocation = frameworkReferenceLocation ++ "FSharp.Core.dll"
            frameworkReferenceLocation, fsharpCoreLocation
        else
            let pf = System.Environment.ExpandEnvironmentVariables("%programfiles%")
            let frameworkReferenceLocation = pf ++ @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1"
            let fsharpCoreLocation = pf ++ @"Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.1.0\FSharp.Core.dll"
            frameworkReferenceLocation, fsharpCoreLocation

    let defaultReferenceAssemblies = [
        "mscorlib.dll"
        "System.Core.dll"
        "System.dll"
        "System.Numerics.dll"
        "Facades" ++ "System.Collections.Concurrent.dll"
        "Facades" ++ "System.Collections.dll"
        "Facades" ++ "System.ComponentModel.Annotations.dll"
        "Facades" ++ "System.ComponentModel.dll"
        "Facades" ++ "System.ComponentModel.EventBasedAsync.dll"
        "Facades" ++ "System.Diagnostics.Contracts.dll"
        "Facades" ++ "System.Diagnostics.Debug.dll"
        "Facades" ++ "System.Diagnostics.Tools.dll"
        "Facades" ++ "System.Diagnostics.Tracing.dll"
        "Facades" ++ "System.Dynamic.Runtime.dll"
        "Facades" ++ "System.Globalization.dll"
        "Facades" ++ "System.IO.dll"
        "Facades" ++ "System.Linq.dll"
        "Facades" ++ "System.Linq.Expressions.dll"
        "Facades" ++ "System.Linq.Parallel.dll"
        "Facades" ++ "System.Linq.Queryable.dll"
        "Facades" ++ "System.Net.NetworkInformation.dll"
        "Facades" ++ "System.Net.Primitives.dll"
        "Facades" ++ "System.Net.Requests.dll"
        "Facades" ++ "System.ObjectModel.dll"
        "Facades" ++ "System.Reflection.dll"
        "Facades" ++ "System.Reflection.Emit.dll"
        "Facades" ++ "System.Reflection.Emit.ILGeneration.dll"
        "Facades" ++ "System.Reflection.Emit.Lightweight.dll"
        "Facades" ++ "System.Reflection.Extensions.dll"
        "Facades" ++ "System.Reflection.Primitives.dll"
        "Facades" ++ "System.Resources.ResourceManager.dll"
        "Facades" ++ "System.Runtime.dll"
        "Facades" ++ "System.Runtime.Extensions.dll"
        "Facades" ++ "System.Runtime.InteropServices.dll"
        "Facades" ++ "System.Runtime.InteropServices.WindowsRuntime.dll"
        "Facades" ++ "System.Runtime.Numerics.dll"
        "Facades" ++ "System.Runtime.Serialization.Json.dll"
        "Facades" ++ "System.Runtime.Serialization.Primitives.dll"
        "Facades" ++ "System.Runtime.Serialization.Xml.dll"
        "Facades" ++ "System.Security.Principal.dll"
        "Facades" ++ "System.ServiceModel.Http.dll"
        "Facades" ++ "System.ServiceModel.Primitives.dll"
        "Facades" ++ "System.ServiceModel.Security.dll"
        "Facades" ++ "System.Text.Encoding.dll"
        "Facades" ++ "System.Text.Encoding.Extensions.dll"
        "Facades" ++ "System.Text.RegularExpressions.dll"
        "Facades" ++ "System.Threading.dll"
        "Facades" ++ "System.Threading.Tasks.dll"
        "Facades" ++ "System.Threading.Tasks.Parallel.dll"
        "Facades" ++ "System.Threading.Timer.dll"
        "Facades" ++ "System.Xml.ReaderWriter.dll"
        "Facades" ++ "System.Xml.XDocument.dll"
        "Facades" ++ "System.Xml.XmlSerializer.dll"]

    let nonMonoReferenceAssemblies = [
        "Facades" ++ "System.ServiceModel.Duplex.dll"
        "Facades" ++ "System.ServiceModel.NetTcp.dll"]

    let defaultReferences =
        seq { yield fsharpCoreLocation
              yield! defaultReferenceAssemblies 
                     |> List.map(fun l ->                     
                            (Path.Combine(frameworkReferenceLocation,l)))
              if not isMono then
                    yield! nonMonoReferenceAssemblies
                           |> List.map(fun l ->
                                (Path.Combine(frameworkReferenceLocation,l)))}    
        |> Seq.toList

    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let getFsiSession() =
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)                        
        let argv = [| "fsi.exe"; "--nologo"; "--debug:full"; "--define:MIXIN"; |]        
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)

    let fsc = SimpleSourceCodeServices()

    let state = new Dictionary<_,_>()
    
    let internalCompile asmName moduleName (metaprogram) metaprogramParams sourceFile dllFile (generationMode:GenerationMode) = 
        let getMatches input regex =
            RegularExpressions.Regex.Matches(input,regex)
            |> Seq.cast<RegularExpressions.Match> 
            |> Seq.map(fun m ->m.Value) 
            |> Seq.toList

        let preparedProgram source =
            let referenceRegex = """#r @?(".+?")+"""
            let includeRegex = """#I @?(".+?")+"""
            let currentDir = FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName
            let rmatches = getMatches source referenceRegex |> List.map(fun r -> r.Replace("#r","").Replace("@","").Replace("\"","").Trim())
            //reverse list so currentDir is always checked after user specified ones
            let imatches = getMatches source includeRegex 
                           |> List.map(fun i -> 
                            if Directory.Exists i then i 
                            else Path.GetFullPath(Path.Combine(currentDir,i)))
                           |> fun i -> currentDir :: i
                           |> List.rev
                           |> List.map(fun r -> r.Replace("#r","").Replace("@","").Replace("\"","").Trim())
            //attempt to find a full path to each referenced assembly by combining
            //each resolution path in turn, picking the first one that matches
            let references =
                rmatches
                |> List.map(fun r -> 
                    imatches 
                    |> List.tryPick(fun i -> 
                        let p = Path.GetFullPath(Path.Combine(i,r))
                        if File.Exists p then Some p
                        else None)
                    |> function 
                        | Some p -> p
                        | None-> 
                            failwithf "Error, could not find referenced assembly %s in any of the specified resolution paths : %A" 
                                      r imatches
                )

            let source =
                match generationMode with
                | GenerationMode.AutoOpenModule -> sprintf "[<AutoOpen>]module %s\n%s" moduleName source
                | GenerationMode.Module -> sprintf "[<AutoOpen>]module %s\n%s" moduleName source
                | GenerationMode.Namespace -> sprintf "namespace %s\n%s" moduleName source
                | failwith ->"impossible"
            source, references

        if File.Exists sourceFile then File.Delete sourceFile
        if File.Exists dllFile then File.Delete dllFile
                        
        let generateReferences references = List.fold(fun acc l -> "-r" :: l :: acc) [] (defaultReferences @ references)
        let args output references = 
            [ "fsc.exe"; "-o"; output; "-a"; sourceFile; "--noframework"; "--validate-type-providers"; "--fullpaths"; "--flaterrors"; "--debug:full" ] 
            @ generateReferences references
            |> List.toArray
                               
        
        try       
            sbErr.Clear() |> ignore
            sbOut.Clear() |> ignore
            // stage 1 - evaluate metaprogram with FSI session
            let fsi, expr =
                match metaprogram with
                | File filePath -> 
                    // for some reason, trying including the file with --use in the fsi session's arguments
                    // simply does not work when hosted interactively - it just does nothing.  This is a bit
                    // of a pain, instead we will have to load the script and then call generate via the 
                    // implict module name that fsi creates, which is a title cased version of the filename
                    let fsi = getFsiSession()
                    fsi.EvalScript filePath
                    let title = Path.GetFileNameWithoutExtension filePath
                    let title = string(Char.ToUpper(title.[0])) + title.Substring(1)                    
                    let expr = 
                        if String.IsNullOrWhiteSpace metaprogramParams then 
                            sprintf "%s.generate()" title
                        else 
                            sprintf "%s.generate %s" title metaprogramParams
                    fsi, expr
                | Text sourceCode -> 
                    let fsi = getFsiSession()
                    fsi.EvalInteraction sourceCode
                    let expr = 
                        if String.IsNullOrWhiteSpace metaprogramParams then 
                            "generate()"
                        else 
                            sprintf "generate %s" metaprogramParams
                    fsi, expr
            
            // stage 2 - attempt to generate code from metaprogram 
            match fsi.EvalExpression expr with
            | Some x -> 
                // stage 2 - take resulting program and compile it with fsc
                let source = x.ReflectionValue :?> string
                let source, refs = preparedProgram source
                File.WriteAllText(sourceFile, source)
                let args = args dllFile refs
                let errors, code = fsc.Compile args
                if code > 0 then Bad (Exception(String.Join("\n", errors))) else
                let asm = Assembly.LoadFrom dllFile
                if state.ContainsKey asmName then state.Remove asmName |> ignore
                state.Add(asmName, {assemblyLocation = dllFile; assembly = asm; metaprogramParams = metaprogramParams })
                Good asm
            | _ -> 
                Bad (new Exception("metaprogram failed to evaluate the generate function:\n" + sbErr.ToString()))
        with
        | ex -> Bad (Exception("metaprogram failed to load:\n" + sbErr.ToString(), ex))
                                
    member this.Compile(metaprogram,moduleName,mode,outputLoc,metaprogramParams,generationMode) =
        let asmName = sprintf "%s, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" moduleName
        let current = FileInfo(Assembly.GetExecutingAssembly().Location).Directory
        let metaFileLoc = try Path.Combine(current.FullName,metaprogram) with _ -> ""
        let fsFile, dllFile = 
            let name =
                if String.IsNullOrWhiteSpace outputLoc then Path.Combine(current.FullName,moduleName)        
                else
                //resolve relative path name
                // note if they put an absolute path in like C:\\temp when you combine
                // with current.FullName you get back the C:\\temp - result!
                let path = Path.GetFullPath(Path.Combine(current.FullName,outputLoc))
                Path.Combine(path,moduleName)
            let fsFile = Path.ChangeExtension(name, ".fs")
            let dllFile = Path.ChangeExtension(name, ".dll")          
            fsFile,dllFile
        
        let source =
            try if  (not (String.IsNullOrWhiteSpace metaFileLoc)) && File.Exists metaFileLoc then File metaFileLoc else Text metaprogram
            with _ -> Text metaprogram
         
        match mode with 
        | CompileMode.CompileWhenMissisng ->
            if state.ContainsKey asmName then state.[asmName].assembly
            else
                if File.Exists dllFile then 
                    let asm = Assembly.LoadFrom dllFile
                    state.Add(asmName, {assemblyLocation = dllFile; assembly = asm;  metaprogramParams = metaprogramParams; })
                    asm
                else 
                    match internalCompile asmName moduleName source metaprogramParams fsFile dllFile generationMode with
                    | Good asm -> asm
                    | Bad ex -> raise ex
        | CompileMode.AlwaysCompile ->
            //let metaprogram = getSource()
            match internalCompile asmName moduleName source metaprogramParams fsFile dllFile generationMode with
            | Good asm -> asm
            | Bad ex -> raise ex
//        | CompileMode.CompileWhenChanged -> 
//            //let metaprogram = getSource()
//            match state.TryGetValue asmName with
//            | true, value when value.originalSource = metaprogram 
//                            && value.assemblyLocation = dllFile 
//                            && value.metaprogramParams = metaprogramParams ->
//                state.[asmName].assembly
//            | _ ->  match internalCompile asmName moduleName metaprogram metaprogramParams fsFile dllFile with
//                    | Good asm -> asm
//                    | Bad ex -> raise ex
        
        | _ -> failwith "impossible"
        
    
