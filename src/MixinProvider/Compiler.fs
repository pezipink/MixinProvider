module MixinCompiler

open System.IO
open Microsoft.FSharp.Compiler.Interactive.Shell
open System.Text
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System
open System.Collections.Generic
open System.Reflection

type CompilationResponse =
    | CompilationSuccessful of Assembly
    | CompilationFail of Exception

type EvaluationResponse =
    | EvaluationSuccessful of program : string
    | EvaluationFailed of Exception

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
    | Other // this is for mixin tp extensions 

type WrapperType =
    | Module of string
    | AutoOpenModule of string
    | Namespace of string
    with 
    member this.ModuleName = 
        match this with
        | Module s 
        | AutoOpenModule s
        | Namespace s -> s

type Environment = {
    /// name and type of the wrapper module or namespace
    /// this will be the type alias the user created the TP with
    wrapperType : WrapperType
    /// metaprogram type and content
    metaprogram : SourceType
    /// static parameters for the metaprogram as a string
    metaprogramParams : string
    /// source file to write the pre-compiled program to
    sourceFile : string
    /// dll flie where the compiled program is written to
    dllFile : string
}

let isMono = Type.GetType("Mono.Runtime") <> null

let evaluateWithFsi env =
    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let fsi =
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)                        
        let argv = [| "fsi.exe"; "--nologo"; "--debug:full"; "--define:MIXIN"; |]        
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
    try
        let expr =
            match env.metaprogram with
            | File filePath -> 
                // for some reason, trying including the file with --use in the fsi session's arguments
                // simply does not work when hosted interactively - it just does nothing.  This is a bit
                // of a pain, instead we will have to load the script and then call generate via the 
                // implict module name that fsi creates, which is a Title cased version of the filename
                fsi.EvalScript filePath
                let title = Path.GetFileNameWithoutExtension filePath
                let title =
                    if title.Length > 1 then string(Char.ToUpper(title.[0])) + title.Substring(1)
                    else string(Char.ToUpper(title.[0]))                
                if String.IsNullOrWhiteSpace env.metaprogramParams then 
                    sprintf "%s.generate()" title
                else 
                    sprintf "%s.generate %s" title env.metaprogramParams
            | Text sourceCode -> 
                fsi.EvalInteraction sourceCode
                if String.IsNullOrWhiteSpace env.metaprogramParams then 
                    "generate()"
                else 
                    sprintf "generate %s" env.metaprogramParams
            | Other -> failwith "The metaprogram mode 'Other' is not supported in this type provider\nPlease make sure you have specified a valid metaprogram literal or location in the static parameters."
        match fsi.EvalExpression expr with
        | Some x -> 
            let source = x.ReflectionValue :?> string
            EvaluationSuccessful(source)
        | _ -> 
            EvaluationFailed(new Exception("metaprogram failed to evaluate the generate function:\n" + sbErr.ToString()))
    with
    | ex -> EvaluationFailed(Exception("metaprogram failed to load:\n" + sbErr.ToString(), ex))
   
let fscCompile env source = 
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

    let fsc = SimpleSourceCodeServices()
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
            match env.wrapperType with
            | WrapperType.AutoOpenModule moduleName -> sprintf "[<AutoOpen>]module %s\n%s" moduleName source
            | WrapperType.Module moduleName -> sprintf "module %s\n%s" moduleName source
            | WrapperType.Namespace moduleName -> sprintf "namespace %s\n%s" moduleName source
        source, references
                   
    let generateReferences references = List.fold(fun acc l -> "-r" :: l :: acc) [] (defaultReferences @ references)
    let args output references = 
        [ "fsc.exe"; "-o"; output; "-a"; env.sourceFile; "--noframework"; "--validate-type-providers"; "--fullpaths"; "--flaterrors"; "--debug:full" ] 
        @ generateReferences references
        |> List.toArray
    
    try                           
        try
            if File.Exists env.sourceFile then File.Delete env.sourceFile
            if File.Exists env.dllFile then File.Delete env.dllFile
        with
        | _ -> raise <| Exception("Could not delete old assembly. If you are running in visual studio, you must not have the source file where you create the type provider open, as the background compiler will lock the assembly (this is mostly a problem when you are using CompileAlways mode). Close the source file, restart the IDE and compile the project to avoid this.")  
        let source, refs = preparedProgram source
        File.WriteAllText(env.sourceFile,source)
        let args = args env.dllFile refs
        let errors, code = fsc.Compile args
        if code > 0 then CompilationFail(Exception(String.Join("\n",errors))) else
        let asm = Assembly.LoadFrom env.dllFile
        CompilationSuccessful asm
    with
    | ex -> CompilationFail ex
    
type MixinCompiler() =
    let state = new Dictionary<_,MixinData>()
    
    let internalCompile env evaluation compilation =
        match evaluation env with
        | EvaluationSuccessful programs ->
            match compilation env programs with
            | CompilationSuccessful asm -> 
                if state.ContainsKey asm.FullName then state.Remove asm.FullName |> ignore
                state.Add(asm.FullName,{assemblyLocation=env.dllFile; assembly=asm; metaprogramParams = env.metaprogramParams})
                asm
            | CompilationFail ex -> raise ex
        | EvaluationFailed ex -> raise ex

                                
    member this.Compile(metaprogram,wrapperType:WrapperType,mode,outputLoc,metaprogramParams,evaluation,compilation) =        
        let asmName = sprintf "%s, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" wrapperType.ModuleName
        let current = FileInfo(Assembly.GetExecutingAssembly().Location).Directory
        let metaFileLoc = try Path.Combine(current.FullName,metaprogram) with _ -> ""
        let fsFile, dllFile = 
            let name =
                if String.IsNullOrWhiteSpace outputLoc then Path.Combine(current.FullName,wrapperType.ModuleName)        
                else
                //resolve relative path name
                // note if they put an absolute path in like C:\\temp when you combine
                // with current.FullName you get back the C:\\temp - result!
                let path = Path.GetFullPath(Path.Combine(current.FullName,outputLoc))
                Path.Combine(path,wrapperType.ModuleName)
            let fsFile = Path.ChangeExtension(name, ".fs")
            let dllFile = Path.ChangeExtension(name, ".dll")          
            fsFile,dllFile
        
        let source =
            if String.IsNullOrWhiteSpace metaprogram then Other
            else try if File.Exists metaFileLoc then File metaFileLoc else Text metaprogram
                 with _ -> Text metaprogram
         
        let env = {
            wrapperType = wrapperType
            metaprogram = source
            metaprogramParams = metaprogramParams
            sourceFile = fsFile
            dllFile = dllFile }

        match mode with 
        | CompileMode.CompileWhenMissisng ->
            if state.ContainsKey asmName && (state.[asmName].metaprogramParams = metaprogramParams) then state.[asmName].assembly
            else
                if File.Exists dllFile then 
                    let asm = Assembly.LoadFrom dllFile
                    state.Add(asmName, {assemblyLocation = dllFile; assembly = asm;  metaprogramParams = metaprogramParams; })
                    asm
                else 
                    internalCompile env evaluation compilation
        | CompileMode.AlwaysCompile ->internalCompile env evaluation compilation
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
        
    
