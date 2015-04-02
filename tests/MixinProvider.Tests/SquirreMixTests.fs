module MixinProvider.SquirrelMix.Tests

open MixinProvider
open NUnit.Framework
open System.Text
open System.Diagnostics

let cleanActual (s:string) = s.TrimEnd().Replace("\r\n","\n")

[<Test>]
let ``create module works`` () =
    let sb = new StringBuilder()
    (0,sb)
    ||> cmodule "TestModule"[ clet "x" (ap "1");] 
    |> ignore

    let expected = """module TestModule =
    let x = 1"""

    Assert.AreEqual(cleanActual(sb.ToString()),expected)


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
    Assert.AreEqual(cleanActual actual,expected, sprintf "expected:\n%s\nactual:\n%s" expected actual)


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

    Assert.AreEqual(cleanActual actual,expected, sprintf "expected:\n%s\nactual:\n%s" expected actual)

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


