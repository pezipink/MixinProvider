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

type mixin_gen() = inherit obj()

[<TypeProvider>]
type MixinProvider() =
    static let mix = new MixinCompiler()
    let invalidation = new Event<EventHandler, EventArgs>()

    let resolveMetaprogram(metaprogram) =
         let fi = FileInfo(Assembly.GetExecutingAssembly().Location)
         try
             if File.Exists metaprogram then metaprogram else
             let fn = Path.GetFullPath(Path.Combine(fi.DirectoryName, metaprogram))               
             if File.Exists fn then fn
             else metaprogram
         with _ -> metaprogram

    abstract member ResolveType : string -> Type
    default x.ResolveType(typeName) = typeof<mixin_gen>

    abstract member AvailableTypes : unit ->Type array
    default x.AvailableTypes() = [| typeof<mixin_gen> |]
            
//    abstract member ExecuteMixinCompile : Type * string array * string * string * MixinCompiler.CompileMode * string * string -> Type 
    member x.ExecuteMixinCompile(typeWithoutArguments,typePathWithArguments:string[],metaprogram,moduleName,compileMode,outputLoc,mpParams,generationMode) = 
        lock mix (fun _ -> 
            try
                let asm = mix.Compile(metaprogram,moduleName,compileMode,outputLoc,mpParams,generationMode) 
                if x.AvailableTypes() |> Array.exists((=) typeWithoutArguments) then
                    asm.GetType(typePathWithArguments.[typePathWithArguments.Length-1])
                else asm.GetType(typeWithoutArguments.FullName)
            with
            | ex -> failwithf "! %s" (ex.ToString()))
    
    abstract member ApplyStaticArgs : Type * string array * obj array -> Type
    default x.ApplyStaticArgs(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
        let mpParams = staticArguments.[1] :?>  string
        let compileMode = staticArguments.[2] :?> CompileMode
        let generationMode = staticArguments.[3] :?>  GenerationMode
        let outputLoc = staticArguments.[4] :?>  string
        let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
        let metaprogram = resolveMetaprogram(staticArguments.[0] :?> string)
        x.ExecuteMixinCompile(typeWithoutArguments, typePathWithArguments, metaprogram, moduleName, compileMode, outputLoc, mpParams,generationMode)
 
    abstract member StaticParameters : unit -> ParameterInfo array
    default x.StaticParameters() =
        [| 
            helpers.stringParameter "metaprogram" None;                
            helpers.stringParameter "metaprogramParameters" (Some "");
            helpers.genericOptionalParameter "compileMode" CompileMode.CompileWhenMissisng
            helpers.genericOptionalParameter "generationMode" GenerationMode.AutoOpenModule
            helpers.stringParameter "outputLocation" (Some "")
        |]

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
            | _ -> failwith ("MixinProvider.GetInvokerExpression: not a ProvidedMethod/ProvidedConstructor/ConstructorInfo/MethodInfo, name=" + syntheticMethodBase.Name + " class=" + syntheticMethodBase.GetType().FullName)

        
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