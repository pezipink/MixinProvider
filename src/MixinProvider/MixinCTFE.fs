namespace MixinProvider

open Microsoft.FSharp.Core.CompilerServices
open System
open System.Collections.Generic
open System.Reflection
open System.IO
open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Quotations
open MixinCompiler

type mixin_ctfe() = inherit obj()

[<TypeProvider>]
type MixinCTFEProvider() = 
    inherit MixinBase<string*string>()

    let createEnv staticParams =
        // no speical params for CTFE, just the dll/fs names
        {staticParams = staticParams
         metaprogram = helpers.resolveMetaprogram staticParams.metaprogram
         additionalData = helpers.getOutputFiles staticParams }

    override this.AvailableTypes() = [| typeof<mixin_ctfe> |]

    override this.ResolveType(_) = typeof<mixin_ctfe>

    override this.StaticParameters() = 
        [| 
            helpers.stringParameter "metaprogram" None;                
            helpers.stringParameter "metaprogramParameters" (Some "");
            helpers.genericOptionalParameter "compileMode" CompileMode.CompileWhenMissisng
            helpers.genericOptionalParameter "generationMode" GenerationMode.AutoOpenModule
            helpers.stringParameter "outputLocation" (Some "")
        |]

    override this.ApplyStaticArgs(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []): Type = 
        let mpParams = staticArguments.[1] :?>  string
        let compileMode = staticArguments.[2] :?> CompileMode
        let generationMode = staticArguments.[3] :?>  GenerationMode
        let outputLoc = staticArguments.[4] :?>  string
        let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
        let metaprogram = staticArguments.[0] :?> string
        let wrapperType =
            match generationMode with
            | GenerationMode.Module -> WrapperType.Module moduleName
            | GenerationMode.AutoOpenModule -> WrapperType.AutoOpenModule moduleName
            | GenerationMode.Namespace -> WrapperType.Namespace moduleName
            | _ -> failwith "impossible"
        let env = createEnv {metaprogram=metaprogram;mpParams=mpParams; wrapperType=wrapperType; compileMode=compileMode;outputLoc=outputLoc}        
        this.ExecuteMixinCompile(typeWithoutArguments,typePathWithArguments,env)

    
    override this.PreEvaluate env =         
        let real = File.Exists (snd env.additionalData)
        if env.staticParams.compileMode = CompileMode.CompileWhenMissisng && real then false else
        true
    
    override this.Evaluate env =
        MixinCompiler.evaluateWithFsi env
    
    override this.PreCompile(program,env) = true
//        match env.staticParams.compileMode with
//        | CompileMode.CompileWhenDifferent when File.Exists(fst env.additionalData) -> 
//            if File.ReadAllText(snd env.additionalData) = program then 
//              this.TryGetOrLoadAsm(env.staticParams.wrapperType.ModuleName) (snd env.additionalData)|>ignore
//              false
//            else true
//        | _ -> true
            
    override this.GetAsm(env) = 
         (this.TryGetOrLoadAsm env.staticParams.wrapperType.ModuleName (snd env.additionalData)).Value

    override this.Compile(program,env) =
        match MixinCompiler.fscCompile program (fst env.additionalData) (snd env.additionalData) env.staticParams.wrapperType with
        | CompilationFail ex -> raise ex
        | CompilationSuccessful asmFile -> asmFile            
            
        