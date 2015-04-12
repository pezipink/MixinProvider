module MixinCompiler

open System.IO
open Microsoft.FSharp.Compiler.Interactive.Shell
open System.Text
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open System
open System.Collections.Generic
open System.Reflection

type CompilationResponse =
    | CompilationSuccessful of string
    | CompilationFail of Exception

type EvaluationResponse =
    | EvaluationSuccessful of program : string
    | EvaluationFailed of Exception

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
    // AlwaysEvaluate will run the whole compilation pipeline
    // if a matching output assembly is not found, otherwise it will
    // always run the evaluation stage but not the compilation.
    // This mode is primarily intedend for the injection mode or
    // other uses that cause side effects with metaprograms but only
    // generate dummy assemblies.
    | AlwaysEvaluate = 2

    | CompileWhenDifferent = 3
    
type GenerationMode =
    /// Default, would be used for most mixins
    | AutoOpenModule = 0
    /// Use this if you don't want to pollute your namespace
    | Module = 1
    /// Namespace is for generating type providers or if you 
    /// Just want types in a namespace.
    | Namespace = 2


type MetaprogramType =
    | File of fileName : string * source : string 
    | Text of source : string
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

type StaticParams = {
    metaprogram : string
    mpParams    : string
    wrapperType : WrapperType
    compileMode : CompileMode
    outputLoc   : string
}

type Environment<'a> = {
    staticParams : StaticParams
    metaprogram  : MetaprogramType  
    additionalData : 'a
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
            | File(filePath,source) -> 
                // for some reason, trying including the file with --use in the fsi session's arguments
                // simply does not work when hosted interactively - it just does nothing.  This is a bit
                // of a pain, instead we will have to load the script and then call generate via the 
                // implict module name that fsi creates, which is a Title cased version of the filename
                fsi.EvalScript filePath
                let title = Path.GetFileNameWithoutExtension filePath
                let title =
                    if title.Length > 1 then string(Char.ToUpper(title.[0])) + title.Substring(1)
                    else string(Char.ToUpper(title.[0]))                
                if String.IsNullOrWhiteSpace env.staticParams.mpParams then 
                    sprintf "%s.generate()" title
                else 
                    sprintf "%s.generate %s" title env.staticParams.mpParams
            | Text sourceCode -> 
                fsi.EvalInteraction sourceCode
                if String.IsNullOrWhiteSpace env.staticParams.mpParams then 
                    "generate()"
                else 
                    sprintf "generate %s" env.staticParams.mpParams
            | Other -> failwith "The metaprogram mode 'Other' is not supported in this type provider\nPlease make sure you have specified a valid metaprogram literal or location in the static parameters."
        match fsi.EvalExpression expr with
        | Some x -> 
            let source = x.ReflectionValue :?> string
            EvaluationSuccessful(source), env
        | _ -> 
            EvaluationFailed(new Exception("metaprogram failed to evaluate the generate function:\n" + sbErr.ToString())), env
    with
    | ex -> EvaluationFailed(Exception("metaprogram failed to load:\n" + sbErr.ToString(), ex)), env

let getWrappedSource source wrapperType =
    match wrapperType with
    | WrapperType.AutoOpenModule moduleName -> sprintf "[<AutoOpen>]module %s\n%s" moduleName source
    | WrapperType.Module moduleName -> sprintf "module %s\n%s" moduleName source
    | WrapperType.Namespace moduleName -> sprintf "namespace %s\n%s" moduleName source
   
let fscCompile program destFile destDll wrapperType  = 
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

        let source = getWrappedSource source wrapperType 
        source, references
                   
    let generateReferences references = List.fold(fun acc l -> "-r" :: l :: acc) [] (defaultReferences @ references)
    let args references = 
        [ "fsc.exe"; "-o"; destDll; "-a"; destFile; "--noframework"; "--validate-type-providers"; "--fullpaths"; "--flaterrors"; "--debug:full" ] 
        @ generateReferences references
        |> List.toArray
    
    try                           
        try
            if File.Exists destFile then File.Delete destFile
            if File.Exists destDll then File.Delete destDll
        with
        | _ -> raise <| Exception("Could not delete old assembly. If you are running in visual studio, you must not have the source file where you create the type provider open, as the background compiler will lock the assembly (this is mostly a problem when you are using CompileAlways mode). Close the source file, restart the IDE and compile the project to avoid this.")  
        let source, refs = preparedProgram program
        File.WriteAllText(destFile,source)
        let args = args refs
        let errors, code = fsc.Compile args
        if code > 0 then CompilationFail(Exception(String.Join("\n",errors))) else
        CompilationSuccessful destDll
    with
    | ex -> CompilationFail ex
    

