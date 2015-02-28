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
    { originalSource : string // original metaprogram before any modifications
      assemblyLocation : string
      metaprogramParams : string
      assembly : Assembly }
 

// this is an enum so it can be used as a static param to the tp
type CompileMode =
    /// Intelligent compilation mode will only re-compile 
    /// assemblies when a change in the metaprogram is detected
    | IntelligentCompile = 0
    /// AlwaysCompile will force assemblies to be re-compiled 
    /// every time the type provider fires - be warned this can 
    /// have a large overhead due to the background compiler.
    /// This option is reccomended as a temporary switch if you
    /// wish to force recompilation for some reason
    | AlwaysCompile = 1
    /// Never compile mode will assume the generated assemblies 
    /// already exist on the disk in the calculated location. 
    /// Use this if you want to permenantly prevent code-generation.
    /// This does not affect the use of generated types via the 
    /// type provider in "mixin-lite" mode.
    | NeverCompile = 2
    

type MixinCompiler() =
    // TODO : Mono support
    let pf = System.Environment.ExpandEnvironmentVariables("%programfiles%")
    let frameworkReferenceLocation = Path.Combine(pf,@"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1")
    let fsharpCoreLocation = Path.Combine(pf, @"Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.1.0\FSharp.Core.dll")
    let defaultReferenceAssemblies = [
        @"mscorlib.dll"
        @"System.Core.dll"
        @"System.dll"
        @"System.Numerics.dll"
        @"Facades\System.Collections.Concurrent.dll"
        @"Facades\System.Collections.dll"
        @"Facades\System.ComponentModel.Annotations.dll"
        @"Facades\System.ComponentModel.dll"
        @"Facades\System.ComponentModel.EventBasedAsync.dll"
        @"Facades\System.Diagnostics.Contracts.dll"
        @"Facades\System.Diagnostics.Debug.dll"
        @"Facades\System.Diagnostics.Tools.dll"
        @"Facades\System.Diagnostics.Tracing.dll"
        @"Facades\System.Dynamic.Runtime.dll"
        @"Facades\System.Globalization.dll"
        @"Facades\System.IO.dll"
        @"Facades\System.Linq.dll"
        @"Facades\System.Linq.Expressions.dll"
        @"Facades\System.Linq.Parallel.dll"
        @"Facades\System.Linq.Queryable.dll"
        @"Facades\System.Net.NetworkInformation.dll"
        @"Facades\System.Net.Primitives.dll"
        @"Facades\System.Net.Requests.dll"
        @"Facades\System.ObjectModel.dll"
        @"Facades\System.Reflection.dll"
        @"Facades\System.Reflection.Emit.dll"
        @"Facades\System.Reflection.Emit.ILGeneration.dll"
        @"Facades\System.Reflection.Emit.Lightweight.dll"
        @"Facades\System.Reflection.Extensions.dll"
        @"Facades\System.Reflection.Primitives.dll"
        @"Facades\System.Resources.ResourceManager.dll"
        @"Facades\System.Runtime.dll"
        @"Facades\System.Runtime.Extensions.dll"
        @"Facades\System.Runtime.InteropServices.dll"
        @"Facades\System.Runtime.InteropServices.WindowsRuntime.dll"
        @"Facades\System.Runtime.Numerics.dll"
        @"Facades\System.Runtime.Serialization.Json.dll"
        @"Facades\System.Runtime.Serialization.Primitives.dll"
        @"Facades\System.Runtime.Serialization.Xml.dll"
        @"Facades\System.Security.Principal.dll"
        @"Facades\System.ServiceModel.Duplex.dll"
        @"Facades\System.ServiceModel.Http.dll"
        @"Facades\System.ServiceModel.NetTcp.dll"
        @"Facades\System.ServiceModel.Primitives.dll"
        @"Facades\System.ServiceModel.Security.dll"
        @"Facades\System.Text.Encoding.dll"
        @"Facades\System.Text.Encoding.Extensions.dll"
        @"Facades\System.Text.RegularExpressions.dll"
        @"Facades\System.Threading.dll"
        @"Facades\System.Threading.Tasks.dll"
        @"Facades\System.Threading.Tasks.Parallel.dll"
        @"Facades\System.Threading.Timer.dll"
        @"Facades\System.Xml.ReaderWriter.dll"
        @"Facades\System.Xml.XDocument.dll"
        @"Facades\System.Xml.XmlSerializer.dll"]

    let referenceArguments =
        seq { yield fsharpCoreLocation
              yield! defaultReferenceAssemblies 
                     |> List.map(fun l ->                     
                            (Path.Combine(frameworkReferenceLocation,l))) }    
        |> Seq.toList
    let sbOut = new StringBuilder()
    let sbErr = new StringBuilder()
    let getFsiSession() =
        
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)                
        let argv = [| "fsi.exe" |]
        let allArgs = Array.append argv [|"--debug+"|]
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream)

    let scs = SimpleSourceCodeServices()
    let id = Guid.NewGuid()

    let state = new Dictionary<_,MixinData>()
    let internalCompile asmName moduleName (metaprogram:string) metaprogramParams sourceFile dllFile = 
//        let preparedMetaprogram =
//            metaprogram.Split('\n') 
//            |> Seq.toList
//            |> List.rev 
//            |> List.tryFind(fun s-> s.StartsWith "#r" || s.StartsWith "#load") 
//            |> function
//                | Some s -> metaprogram.Replace(s, sprintf "%s\n[<AutoOpen>]module %s=\n" s moduleName )
//                | None -> sprintf "[<AutoOpen>]module %s=\n%s" moduleName metaprogram

        if File.Exists sourceFile then File.Delete sourceFile
        if File.Exists dllFile then File.Delete dllFile
                        
        let references = List.fold(fun acc l -> "-r" :: l :: acc) [] referenceArguments 
        let args o = [ "fsc.exe"; "-o"; o; "-a"; sourceFile; "--noframework"; "--validate-type-providers"; "--fullpaths"; "--flaterrors" ] 
                        @ references
                        |> List.toArray
                               
        let fsi = getFsiSession()     
        try       
            fsi.EvalInteraction metaprogram                            
            match fsi.EvalExpression "generate()" with
            | Some x -> 
                let source = x.ReflectionValue :?> string
                let source = sprintf "[<AutoOpen>]module %s\n%s" moduleName source
                File.WriteAllText(sourceFile, source)
                let args = args dllFile
                let errors, code = scs.Compile args
                if code > 0 then Bad (Exception(String.Join("\n", errors))) else
                let asm = Assembly.LoadFrom dllFile
                if state.ContainsKey asmName then state.Remove asmName |> ignore
                state.Add(asmName, {originalSource=metaprogram; assemblyLocation = dllFile; assembly = asm; metaprogramParams = metaprogramParams })
                Good asm
            | _ -> 
                Bad (new Exception("metaprogram failed"))
        with
        | ex -> Bad (Exception(sbErr.ToString(), ex))
                                
    member this.Compile(metaprogram,moduleName,mode,outputLoc,metaprogramParams) =
        let asmName = sprintf "%s, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" moduleName
        let current = FileInfo(Assembly.GetExecutingAssembly().Location).Directory
        let metaFileLoc = Path.Combine(current.FullName,metaprogram)
        let fsFile, dllFile = 
            let name =
                if String.IsNullOrWhiteSpace outputLoc then Path.Combine(current.FullName,moduleName)        
                else
                //resolve relative path name
                let path = Path.GetFullPath(Path.Combine(current.FullName,outputLoc))
                Path.Combine(current.FullName,moduleName)
            let fsFile = Path.ChangeExtension(name, ".fs")
            let dllFile = Path.ChangeExtension(name, ".dll")          
            fsFile,dllFile
         
        match mode with 
        | CompileMode.NeverCompile ->
            if state.ContainsKey asmName then state.[asmName].assembly
            else
                if File.Exists dllFile then 
                    let asm = Assembly.LoadFrom dllFile
                    state.Add(asmName, {originalSource=""; assemblyLocation = dllFile; assembly = asm;  metaprogramParams = metaprogramParams  })
                    asm
                else failwithf "Compile Mode was set to Never Compile, but the assembly %s could not be found in the cache." asmName
        | CompileMode.AlwaysCompile ->
            let metaprogram = if File.Exists metaFileLoc then File.ReadAllText metaFileLoc else metaprogram
            match internalCompile asmName moduleName metaprogram metaprogramParams fsFile dllFile with
            | Good asm -> asm
            | Bad ex -> raise ex
        | CompileMode.IntelligentCompile -> 
            let metaprogram = if File.Exists metaFileLoc then File.ReadAllText metaFileLoc else metaprogram
            match state.TryGetValue asmName with
            | true, value when value.originalSource = metaprogram 
                            && value.assemblyLocation = dllFile 
                            && value.metaprogramParams = metaprogramParams ->
                state.[asmName].assembly
            | _ ->  match internalCompile asmName moduleName metaprogram metaprogramParams fsFile dllFile with
                    | Good asm -> asm
                    | Bad ex -> raise ex
        
        | _ -> failwith "impossible"
        
    
