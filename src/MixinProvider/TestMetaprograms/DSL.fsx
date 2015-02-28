#load @"F:\GIT\MixinProvider\src\MixinProvider\CodeGenDSL.fs"
#r @"F:\GIT\MixinProvider\bin\MixinProvider.dll"
open System.Text
open MixinProvider.CodeGen
open MixinProvider
type Test2 = mixin_gen< "TestMetaprograms\\basic.fsx" >

let generate() =
    let sb = new StringBuilder()    
     
    (0,sb)
    ||> cmodule "TestModule"
        [
            clet "x" (ap (Test2.x.ToString()));
            clet "Y" (ap "1");
            newli;
            cmodule "TestTypes"
                [
                    crecordType "RecordType" [("x","int");("y","int")] [];
                    crecordType "RecordTypeWithMembers" [("x","int");("y","int")] 
                        [
                            cmember (Instance "this") "" "testX" (Partial ["x",Some "int"]) 
                                (api "{ this with x = x }" )
                            cmember (Instance "this") "" "testY" (Partial ["y",Some "int"]) 
                                (api "{ this with y = y }" )
                    
                        ]; 
                    crecordType "RecordTypeAgain" [("x","int");("y","int")] [];
                ]
        ] 
    |> ignore


    sb.ToString()