module MixinProvider.SquirrelMix.Tests

open MixinProvider
open NUnit.Framework
open System.Text
open System.Diagnostics

let clean (s:string) = s.TrimEnd().Replace("\r\n","\n")

[<Test>]
let ``create module works`` () =
    let sb = new StringBuilder()
    (0,sb)
    ||> cmodule "TestModule"[ clet "x" (ap "1");] 
    |> ignore

    let expected = """module TestModule =
    let x = 1"""

    Assert.AreEqual(clean (sb.ToString()),clean expected)


[<Test>]
let ``nested create module works`` () =
    let sb = new StringBuilder()
    (0,sb)
    ||> cmodule "TestModule"
            [ clet "x" (ap "1");
              cmodule "NestedModule" 
                [
                    clet "y" (ap "2");
                ]
            ] 
    |> ignore
    let actual = sb.ToString()
    let expected = """module TestModule =
    let x = 1
    module NestedModule =
        let y = 2"""
    
    Debug.WriteLine actual
    Debug.WriteLine expected
    Assert.AreEqual(clean actual, clean expected, sprintf "expected:\n%s\nactual:\n%s" expected actual)


[<Test>]
let ``nested create module with initial indent works`` () =
    let sb = new StringBuilder()
    (1,sb)
    ||> cmodule "TestModule"
            [ clet "x" (ap "1");
              cmodule "NestedModule" 
                [
                    clet "y" (ap "2");
                ]
            ] 
    |> ignore
    let actual = sb.ToString()
    let expected = """    module TestModule =
        let x = 1
        module NestedModule =
            let y = 2"""

    Assert.AreEqual(clean actual,clean expected, sprintf "expected:\n%s\nactual:\n%s" expected actual)

[<Test>]
let ``create type with inheritance works`` () = 

    let sb = new StringBuilder()

    let c = 
        ctype "TestProvider" [] "MixinProvider()"
            [
                cmember (InstanceOverride "this") "" "AvailableTypes" NoArgs 
                    (api "[| typeof<x_mixin> |]")
                cmember (InstanceOverride "this") "" "ResolveType" (Tuple ["_",None])  
                    (api "typeof<x_mixin>")
                cmember (InstanceOverride "this") "" "StaticParameters" NoArgs 
                    (carray 
                        [api "MixinProvider.helpers.intParameter \"x\" None";
                         api "MixinProvider.helpers.intParameter \"y\" None";])
                cmember (InstanceOverride "this") "" "ApplyStaticParameters" 
                    (Tuple ["typeWithoutArguments",Some "Type"; 
                            "typePathWithArguments",Some "string []";
                            "staticArguments",Some "obj []";]) 
                    (ccode 
                        [clet "x" (ap "staticArguments.[0] :?> int")                         
                         clet "y" (ap "staticArguments.[1] :?> int")
                         clet "compileMode" (ap "staticArguments.[2] :?> CompileMode")
                         clet "outputLoc" (ap "staticArguments.[3] :?> string")
                         clet "moduleName" (ap "typePathWithArguments.[typePathWithArguments.Length-1]")
                         clet "mparams" (ap "String.Join(\" \", [|x; y|])")
                         api "this.ExecuteMixinCompile(typeWithoutArguments, typePathWithArguments, @\"C:\\Users\\ross\\Documents\\MixinProvider\\src\\MixinProvider\\TestMetaprograms\\Basic_Params.fsx\", moduleName, compileMode, outputLoc, mpParams)"
                         ])
                      
            ]

   
    (0,sb) ||> cmodule "TestModule" [ c ] |> ignore

    let actual = sb.ToString()
    let expected = """module TestModule =
    type TestProvider() =
        inherit MixinProvider()
        with
             override this.AvailableTypes() = 
                [| typeof<x_mixin> |]
             override this.ResolveType(_) = 
                typeof<x_mixin>
             override this.StaticParameters() = 
                [|
                    MixinProvider.helpers.intParameter "x" None;
                    MixinProvider.helpers.intParameter "y" None;
                |]

             override this.ApplyStaticParameters(typeWithoutArguments : Type, typePathWithArguments : string [], staticArguments : obj []) = 
                let x = staticArguments.[0] :?> int
                let y = staticArguments.[1] :?> int
                let compileMode = staticArguments.[2] :?> CompileMode
                let outputLoc = staticArguments.[3] :?> string
                let moduleName = typePathWithArguments.[typePathWithArguments.Length-1]
                let mparams = String.Join(" ", [|x; y|])
                this.ExecuteMixinCompile(typeWithoutArguments, typePathWithArguments, @"C:\Users\ross\Documents\MixinProvider\src\MixinProvider\TestMetaprograms\Basic_Params.fsx", moduleName, compileMode, outputLoc, mpParams)

"""

    Assert.AreEqual(clean actual,clean expected, sprintf "expected:\n%s\nactual:\n%s" expected actual)


[<Test>]
let ``squirrels``() =
    let sb = new StringBuilder()

    
    (0,sb)
    ||> cmodule "TestModule"
        [
            clet "x" (ap "1");
            clet "Y" (ap "1");
            newli;
            cmodule "TestTypes"
                [
                    crecord "RecordType" [("x","int");("y","long")] [];
                    crecord "RecordTypeWithMembers" [("x","int");("y","long")] 
                        [
                            cmember (Instance "this") "private" "test" (Partial ["x",Some "int"]) 
                                (api "{ this with x = x }" )
                            cmember (Instance "this") "private" "test" (Partial ["y",Some "int"]) 
                                (api "{ this with y = y }" )
                    
                        ]; 
                    crecord "RecordType" [("x","int");("y","long")] [];
                ]
        ] 
    |> ignore

    
    Assert.AreEqual(42,42)


