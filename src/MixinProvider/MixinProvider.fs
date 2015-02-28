namespace MixinProvider

open Microsoft.FSharp.Core.CompilerServices
open System
open System.Collections.Generic
open System.Reflection
open System.IO
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Quotations
open MixinCompiler

type mixin_gen() = inherit obj()

module helpers =
    let stringParameter name def =
      { new ParameterInfo() with
        override this.Name with get() = name
        override this.ParameterType with get() = typeof<string>
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = def
        override this.DefaultValue with get() =  def
        override this.Attributes with get() = 
                                    if String.IsNullOrWhiteSpace(def:?>string) then ParameterAttributes.Optional
                                    else ParameterAttributes.None }

    let compileNModeParamter name def =
      { new ParameterInfo() with
        override this.Name with get() = name
        override this.ParameterType with get() = typeof<MixinCompiler.CompileMode>
        override this.Position with get() = 0
        override this.RawDefaultValue with get() = def
        override this.DefaultValue with get() =  def
        override this.Attributes with get() = ParameterAttributes.Optional }

[<TypeProvider>]
type MixinProvider() =
//    static let mut = new System.Threading.Mutex() // wooo a lesser-spotted mutex!
    static let mix = new MixinCompiler()
    let invalidation = new Event<EventHandler, EventArgs>()
    interface ITypeProvider with
        member x.ApplyStaticArguments(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
            //System.Diagnostics.Debugger.Launch()
            let compileMode = staticArguments.[1] :?> CompileMode
            let outputLoc = staticArguments.[2] :?>  string
            let mpParams = staticArguments.[3] :?>  string
            let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
            let metaprogram =
                let arg = staticArguments.[0] :?> string
                let fi = FileInfo(Assembly.GetExecutingAssembly().Location)
                let fn = Path.Combine(fi.DirectoryName, arg)
                if File.Exists fn then fn
                else arg
            
            lock mix (fun _ -> 

                    try
                        let asm = mix.Compile(metaprogram,moduleName,compileMode,outputLoc,mpParams) 
                        if typeWithoutArguments = typeof<mixin_gen> then
                            asm.GetType(typePathWithArguments.[typePathWithArguments.Length-1])
                        else asm.GetType(typeWithoutArguments.FullName)
                    with
                    | ex -> failwithf "! %s" (ex.ToString())

            )
//            if mut.WaitOne(5000) then
//                try                                
//                        let asm = mix.Compile(metaprogram,moduleName,compileMode) 
//                        if typeWithoutArguments = typeof<mixin_gen> then
//                            asm.GetType(typePathWithArguments.[typePathWithArguments.Length-1])
//                        else asm.GetType(typeWithoutArguments.FullName)
//                finally mut.ReleaseMutex()
//            
//            else
//                failwith "timed out waiting on mutex"
            
        
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
            [| helpers.stringParameter "metaprogram" ""; 
               helpers.compileNModeParamter "compileMode" CompileMode.IntelligentCompile
               helpers.stringParameter "outputLocation" ""
               helpers.stringParameter "metaprogramParameters" "";
             |]
        
        [<CLIEvent>]
        member x.Invalidate: IEvent<EventHandler,EventArgs> = 
            invalidation.Publish
        
    interface IProvidedNamespace with
        member x.GetNestedNamespaces(): IProvidedNamespace [] = 
            [||]
        
        member x.GetTypes(): Type [] = 
            [| yield typeof<mixin_gen>; |]
        
        member x.NamespaceName: string = 
            "MixinProvider"
        
        member x.ResolveTypeName(typeName: string): Type = 
            typeof<mixin_gen>
        

[<assembly:TypeProviderAssembly>]
do  
    
    ()