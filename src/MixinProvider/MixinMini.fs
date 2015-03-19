module MixinMini

type Basic_Params_mixin() = inherit obj()

open MixinProvider
open MixinCompiler
open System

type Basic_ParmasMixinProvider() =
    inherit MixinProvider.MixinProvider()
    
    override this.AvailableTypes() = [| typeof<Basic_Params_mixin> |]
    
    override this.ResolveType(_) = typeof<Basic_Params_mixin>

    override this.StaticParameters() = 
        [| MixinProvider.helpers.intParameter "x" None
           MixinProvider.helpers.intParameter "y" None
           MixinProvider.helpers.genericOptionalParameter "compileMode" CompileMode.CompileWhenChanged
           MixinProvider.helpers.stringParameter "outputLocation" (Some "")  |]
    
    override this.ApplyStaticArgs(typeWithoutArguments: Type, typePathWithArguments: string [], staticArguments: obj []) =
        let x = staticArguments.[0] :?> int
        let y = staticArguments.[1] :?> int
        let compileMode = staticArguments.[2] :?> CompileMode
        let outputLoc = staticArguments.[3] :?>  string
        let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
        let mpParams = String.Join(" ", [|x; y|] )
        this.ExecuteMixinCompile(typeWithoutArguments, typePathWithArguments, @"C:\Users\ross\Documents\MixinProvider\src\MixinProvider\TestMetaprograms\Basic_Params.fsx", moduleName, compileMode, outputLoc, mpParams)
 