namespace MixinProvider

open Microsoft.FSharp.Core.CompilerServices
open System
open System.Collections.Generic
open System.Reflection
open System.IO
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Quotations
open MixinCompiler

module helpers =
    let intParameter name (def:int option) =
      let def' = 
        let d = def |> function Some v -> v | _ -> 0
        box d
      { new ParameterInfo() with
        override this.Name with get() = name
        override this.ParameterType with get() = typeof<int>
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = def'
        override this.DefaultValue with get() =  def'
        override this.Attributes 
            with get() =
                match def with 
                | Some v -> ParameterAttributes.Optional
                | _ -> ParameterAttributes.None }
    
    let stringParameter name (def:string option) =
      let def' = 
        let d = def |> function Some v -> v | _ -> "" 
        box d
      { new ParameterInfo() with
        override this.Name with get() = name
        override this.ParameterType with get() = typeof<string>
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = def'
        override this.DefaultValue with get() =  def'
        override this.Attributes 
            with get() =
                match def with 
                | Some v -> ParameterAttributes.Optional
                | _ -> ParameterAttributes.None }
    
    let genericOptionalParameter name def =
      { new ParameterInfo() with
        override this.Name with get() = name
        override this.ParameterType with get() = def.GetType()
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = def
        override this.DefaultValue with get() =  def
        override this.Attributes with get() = ParameterAttributes.Optional }
    
    let resolveMetaprogram(metaprogram) =
         let fi = FileInfo(Assembly.GetExecutingAssembly().Location)
         try
             if File.Exists metaprogram then File(metaprogram, File.ReadAllText metaprogram) else
             let fn = Path.GetFullPath(Path.Combine(fi.DirectoryName, metaprogram))               
             if File.Exists fn then File(fn, File.ReadAllText fn)
             else Text metaprogram
         with _ -> Text metaprogram

    let getOutputFiles staticParams = 
        let current = FileInfo(Assembly.GetExecutingAssembly().Location).Directory
        let name =
            if String.IsNullOrWhiteSpace staticParams.outputLoc then Path.Combine(current.FullName,staticParams.wrapperType.ModuleName)
            else
            //resolve relative path name
            // note if they put an absolute path in like C:\\temp when you combine
            // with current.FullName you get back the C:\\temp. Reeesult!
            let path = Path.GetFullPath(Path.Combine(current.FullName,staticParams.outputLoc))
            Path.Combine(path,staticParams.wrapperType.ModuleName)
        let fsFile = Path.ChangeExtension(name, ".fs")
        let dllFile = Path.ChangeExtension(name, ".dll")          
        fsFile,dllFile

[<AbstractClass>]
// I love a good abstract class in F#
type MixinBase<'a>() =
    static let state = new Dictionary<_,Assembly>()
    let invalidation = new Event<EventHandler, EventArgs>()

    abstract member ResolveType : string -> Type

    abstract member AvailableTypes : unit ->Type array        
    
    abstract member PreEvaluate : Environment<'a> -> bool
    
    abstract member Evaluate : Environment<'a> -> EvaluationResponse * Environment<'a>
    
    abstract member PreCompile : string * Environment<'a> -> bool
    
    abstract member Compile : string * Environment<'a> -> string    

    abstract member GetAsm : Environment<'a> -> Assembly
    
    member this.GetAsmName name = sprintf "%s, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" name

    member this.TryGetAsm asmName = state.TryGetValue asmName

    member this.AsmExists asmName = state.ContainsKey asmName
     
    member this.TryGetAsmFromState env =
        let asmName = this.GetAsmName env.staticParams.wrapperType.ModuleName
        state.TryGetValue asmName
    
    member this.GetAsmFromState env =
        let asmName = this.GetAsmName env.staticParams.wrapperType.ModuleName
        state.[asmName]

    member this.UpdateState (asm:Assembly) =
        if state.ContainsKey(asm.FullName) then state.Remove asm.FullName |> ignore
        state.Add(asm.FullName,asm)

    member this.TryGetOrLoadAsmFromState env =
        this.TryGetOrLoadAsm env.staticParams.wrapperType.ModuleName 

    member this.TryGetOrLoadAsm moduleName dllFile =
        let name = this.GetAsmName moduleName
        match this.TryGetAsm name with
        | true, asm -> Some asm
        | false, _ when File.Exists dllFile ->
            try
                let asm = Assembly.LoadFrom dllFile
                this.UpdateState asm
                Some asm
            with _ -> None
        | _ -> None

    member this.EnsureCompilerServicesReady() =
        //type providers are weird. compiler services is used heavily, but referencing it is not
        //enough, as it depends where you are executing. If in VS and the user is using power tools
        //or another extension that loads the compiler services, it will use that (assuming its a new enough version!)
        //otherwise, we might have to load it ourself.  If this is executing in msbuild for example, or some other
        //locaiton.
        AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.filter(fun a -> a.FullName.StartsWith("FSharp.Compiler.Service,"))
        |> Seq.isEmpty
        |> function 
            | true -> 
                // we can assume the compiler services will be alongside this dll.
                let current = FileInfo(Assembly.GetExecutingAssembly().Location).Directory
                Assembly.LoadFile(Path.Combine(current.FullName,"FSharp.Compiler.Service.dll")) |> ignore
            | _ -> ()

    member this.ExecuteMixinCompile(typeWithoutArguments,typePathWithArguments:string[],env) = 
        lock state (fun _ -> 
            try          
                this.EnsureCompilerServicesReady()      
                let asm =
                    if not (this.PreEvaluate env) then (this.GetAsm env) else
                    let evalResponse,env = this.Evaluate env
                    match evalResponse with
                    | EvaluationFailed ex -> raise ex
                    | EvaluationSuccessful program -> 
                        if not (this.PreCompile(program,env)) then (this.GetAsm env) else
                        let asmFile = this.Compile(program, env) 
                        let asm = Assembly.LoadFrom asmFile
                        this.UpdateState asm
                        asm
                if this.AvailableTypes() |> Array.exists((=) typeWithoutArguments) then
                    asm.GetType(typePathWithArguments.[typePathWithArguments.Length-1])
                else asm.GetType(typeWithoutArguments.FullName)
            with
            | ex -> failwithf "! %s" (ex.ToString()))
    
    abstract member ApplyStaticArgs : Type * string array * obj array -> Type
    
    abstract member StaticParameters : unit -> ParameterInfo array
    
    abstract member GetNamespace : unit -> string
    default x.GetNamespace() = "MixinProvider"

    interface ITypeProvider with
        member x.ApplyStaticArguments(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
            x.ApplyStaticArgs(typeWithoutArguments, typePathWithArguments, staticArguments)       
        
        member x.Dispose(): unit = 
            ()
        
        member x.GetGeneratedAssemblyContents(assembly: Reflection.Assembly): byte [] = 
            File.ReadAllBytes assembly.Location
        
        member x.GetInvokerExpression(syntheticMethodBase: Reflection.MethodBase, parameters: Quotations.Expr []): Quotations.Expr = 
            match syntheticMethodBase with
            | :?  ConstructorInfo as cinfo ->  
                Quotations.Expr.NewObject(cinfo, Array.toList parameters) 
            | :? System.Reflection.MethodInfo as minfo ->  
                if minfo.IsStatic then 
                    Quotations.Expr.Call(minfo, Array.toList parameters) 
                else
                    Quotations.Expr.Call(parameters.[0], minfo, Array.toList parameters.[1..])
            | _ -> failwith ("MixinProvider.GetInvokerExpression: not a ConstructorInfo/MethodInfo, name=" + syntheticMethodBase.Name + " class=" + syntheticMethodBase.GetType().FullName)
        
        member x.GetNamespaces(): IProvidedNamespace [] = 
            [| x |]
        
        member x.GetStaticParameters(typeWithoutArguments: Type): Reflection.ParameterInfo [] = 
            x.StaticParameters()

        [<CLIEvent>]
        member x.Invalidate: IEvent<EventHandler,EventArgs> = 
            invalidation.Publish
        
    interface IProvidedNamespace with
        member x.GetNestedNamespaces(): IProvidedNamespace [] = 
            [||]
        
        member x.GetTypes(): Type [] = 
            x.AvailableTypes()
        
        member x.NamespaceName: string = 
            x.GetNamespace()
        
        member x.ResolveTypeName(typeName: string): Type = 
            x.ResolveType(typeName)


[<assembly:TypeProviderAssembly>]
do  
    
    ()