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
open System.Text
open Microsoft.FSharp.Compiler.Interactive.Shell

type mixin_inject() = inherit obj()

// dummy inject functions and attributes
[<AutoOpen>]
module Injection =
    let mixin_inject (injected:bool) = ()
    let mixin_inject1 a (injected:bool) = ()
    let mixin_inject2 a b (injected:bool) = ()
    let mixin_inject3 a b c (injected:bool) = ()
    let mixin_inject4 a b c d (injected:bool) = ()
    let mixin_inject5 a b c d e (injected:bool) = ()
    let mixin_inject_end() = ()

    type mixin_injectAttribute(injected:bool) = inherit Attribute()
    type mixin_inject1Attribute(a,injected:bool) = inherit Attribute()
    type mixin_inject2Attribute(a,b,injected:bool) = inherit Attribute()
    type mixin_inject3Attribute(a,b,c,injected:bool) = inherit Attribute()
    type mixin_inject4Attribute(a,b,c,d,injected:bool) = inherit Attribute()
    type mixin_inject5Attribute(a,b,c,d,e,injected:bool) = inherit Attribute()
    type mixin_inject_endAttribute() = inherit Attribute()

type ProjectSeekMode =
    | Absolute = 1
    | Scan = 2
    | RecursiveScan = 3

[<TypeProvider>]
type MixinInjectionProvider() =
    inherit MixinProvider()
    // some fun concurrent flaming hoops here - the FsChcecker will cause a deadlock
    // if it evaluates a project that also uses the mixin type provider.  
    // to get around this, maintain a concurrent dictionary of projects that are currently
    // being async checked, and if the project is already being checked, we simply
    // allow processing to continue to the compilation stage.  Because the injection 
    // provider only returns a dummy assembly anyway, this works out quite nicely :)
    static let checker = FSharpChecker.Create(keepAssemblyContents=true)
    static let projectsCompiling = System.Collections.Concurrent.ConcurrentDictionary<string,FSharpProjectOptions>()    
    static let getEvalSession env =
        let sbOut = new StringBuilder()
        let sbErr = new StringBuilder()
        
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)                        
        let argv = [| "fsi.exe"; "--nologo"; "--debug:full"; "--define:MIXIN"; |]        
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        let fsi = FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
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
            fsi, title
        | Text sourceCode -> 
            fsi.EvalInteraction sourceCode
            fsi, ""
                    
        | Other -> failwith "The metaprogram mode 'Other' is not supported in this type provider\nPlease make sure you have specified a valid metaprogram literal or location in the static parameters."            
        

    
    static let processResults projectFile (results:FSharpCheckProjectResults) env =        
        let injectionNames =
            ["mixin_inject1"
             "mixin_inject2"
             "mixin_inject3"
             "mixin_inject4"
             "mixin_inject5"
             "mixin_inject_end"
             "mixin_inject1Attribute"
             "mixin_inject2Attribute"
             "mixin_inject3Attribute"
             "mixin_inject4Attribute"
             "mixin_inject5Attribute"
             "mixin_inject_endAttribute"]
       
        let fsi,prefix = getEvalSession env
        results.GetAllUsesOfAllSymbols() |> Async.RunSynchronously
        |> Seq.filter(fun s -> List.exists ((=) s.Symbol.DisplayName) injectionNames)
        |> Seq.groupBy(fun s -> s.FileName)
        |> Seq.map(fun (fn,refs) -> fn, refs |> Seq.sortBy(fun s -> s.RangeAlternate.StartLine) |> Seq.toList)
        |> Seq.map(fun (fn,refs) -> fn, refs |> List.partition(fun s -> s.Symbol.DisplayName.Contains("_end") = false))
        |> Seq.map(fun (fn,refs) -> fn, refs ||> List.zip)
        |> Seq.map(fun (fn,refs) -> 
            let lines = System.IO.File.ReadAllLines fn
            let lastIndex, newLines =
                refs        
                |> List.fold(fun (lastIndex,acc) (start, finish) ->
                    let expr = 
                        if prefix <> "" then
                             sprintf "%s.%s" prefix (lines.[start.RangeAlternate.StartLine-1].Substring(lines.[start.RangeAlternate.StartLine-1].IndexOf("=")+1))                                
                        else
                             sprintf "%s" (lines.[start.RangeAlternate.StartLine-1].Substring(lines.[start.RangeAlternate.StartLine-1].IndexOf("=")+1))
                    let program = 
                        match fsi.EvalExpression(expr) with 
                        | Some x -> 
                            let source = x.ReflectionValue :?> string
                            source.Split([|Environment.NewLine|], StringSplitOptions.None) |> Array.toList
                        | _ -> []            
                    let x = (lines.[lastIndex..start.RangeAlternate.StartLine-1] |> Array.toList) @ program
                    (finish.RangeAlternate.StartLine,x))(0,[])
            fn, newLines @ (lines.[lastIndex-1..] |> Array.toList))
        |> Seq.iter(fun (fn,lines) -> System.IO.File.WriteAllLines(fn,lines))
        
        projectsCompiling.TryRemove(projectFile) |> ignore

    let checkProject env projFile = 
        let projOptions = checker.GetProjectOptionsFromProjectFile projFile 
        match projectsCompiling.TryAdd(projFile,projOptions) with
        | true -> 
            File.AppendAllText("I:\\mixin.log", sprintf "%s: checking project... \n" (System.Diagnostics.Process.GetCurrentProcess().ProcessName ))
            async{
                let! results = checker.ParseAndCheckProject projOptions
                processResults projFile results env
            } |> Async.Start
        | false -> ()

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
                    yield! directories next }
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
        let seekMode, seekPath = env.additionalData
        let results = projectScan seekMode seekPath |> List.map (checkProject env)
        
        EvaluationSuccessful("let result = \"injection process cvompleted successfully\"")

    override this.AvailableTypes() = [| typeof<mixin_inject> |]

    override this.ResolveType(_) = typeof<mixin_inject>

    override this.StaticParameters() = 
        [|
            helpers.stringParameter "injectionMetaprogram" None  
            helpers.stringParameter "projectSeekPath" None
            helpers.genericOptionalParameter "projectSeekMode" ProjectSeekMode.Scan            
        |]

    override this.ApplyStaticArgs(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
        let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
        let metaprogram = helpers.resolveMetaprogram(staticArguments.[0] :?> string)
        let seekPath = staticArguments.[1] :?> string
        let seekMode = staticArguments.[2] :?> ProjectSeekMode
        let wrapperType = WrapperType.Module moduleName
        File.AppendAllText("I:\\mixin.log", sprintf "%s: calling compile... \n" (System.Diagnostics.Process.GetCurrentProcess().ProcessName ))
        this.ExecuteMixinCompile(
            typeWithoutArguments, 
            typePathWithArguments, 
            metaprogram, 
            wrapperType, 
            CompileMode.CompileWhenMissisng, 
            "", 
            "",
            injectMetaprogram,
            MixinCompiler.fscCompile,
            (seekMode,seekPath))

