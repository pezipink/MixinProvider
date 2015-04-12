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

type mixin_full() = inherit obj()

type MixinFullData = {
    dummyFile   : string
    dummyDll    : string
    realFile    : string
    realDll     : string
}

[<TypeProvider>]
type MixinFullProvider() =
    inherit MixinBase<MixinFullData>()

    let createEnv staticParams =
        // no speical params for CTFE, just the dll/fs names
        let realFile, realDll = helpers.getOutputFiles staticParams 
        let dummyFile = realFile.Replace(".fs", "_dummy.fs")
        let dummyDll = realDll.Replace(".dll", "_dummy.dll")
        {staticParams = staticParams
         metaprogram = helpers.resolveMetaprogram staticParams.metaprogram
         additionalData = {dummyFile = dummyFile; dummyDll = dummyDll; realFile = realFile; realDll = realDll } }

    override this.AvailableTypes() = [| typeof<mixin_full> |]

    override this.GetAsm(env) = 
        match this.TryGetAsm(this.GetAsmName(env.staticParams.wrapperType.ModuleName + "_dummy")) with
        | true, asm -> asm
        | _ -> failwith "could not find dummy assembly in cache"

    override this.ResolveType(_) = typeof<mixin_full>

    override this.StaticParameters() = 
        [| 
            helpers.stringParameter "metaprogram" None;                
            helpers.stringParameter "metaprogramParameters" (Some "");
            helpers.genericOptionalParameter "compileMode" CompileMode.CompileWhenDifferent
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
        let real = File.Exists env.additionalData.realDll 
        let dummy = this.TryGetOrLoadAsm(env.staticParams.wrapperType.ModuleName + "_dummy") env.additionalData.dummyDll         
        if env.staticParams.compileMode = CompileMode.CompileWhenMissisng && real && dummy.IsSome then false else
        true
    
    override this.Evaluate env =
        MixinCompiler.evaluateWithFsi env
    
    override this.PreCompile(program,env) =
        if File.Exists(env.additionalData.realDll) = false || File.Exists(env.additionalData.dummyDll) = false then true else
        match env.staticParams.compileMode with
        | CompileMode.CompileWhenDifferent when (File.Exists(env.additionalData.realFile) && File.ReadAllText(env.additionalData.realFile) = getWrappedSource program env.staticParams.wrapperType) -> false
        | _ -> true
            
    override this.Compile(program,env) =
        let dummyDll = 
            // only generate a dummy assembly if it doesn't exist
            // the type provider would have locked this otherwise
            match File.Exists(env.additionalData.dummyDll) with
            | true -> env.additionalData.dummyDll
            | _ ->
                match MixinCompiler.fscCompile "let result = \"full generation completed successfully\"" env.additionalData.dummyFile env.additionalData.dummyDll env.staticParams.wrapperType with
                | CompilationSuccessful asmFile -> asmFile
                | _ -> failwith "should be impossible!"
        
        // we should be able to get away with always generating the real assembly
        // unless the user has #r'd it in FSI, there's nothing we can do about that
        // they will just have to reset fsi.
        match MixinCompiler.fscCompile program (env.additionalData.realFile) (env.additionalData.realDll) env.staticParams.wrapperType with
        | CompilationFail ex -> raise ex
        | CompilationSuccessful asmFile ->
            // we never load the real assembly
            ()
            
        dummyDll
    