module MixinProvider.Mixin.Tests


open NUnit.Framework
open System.Text
open System.Diagnostics

let g = "let generate() = \"let [<Literal>]program = \"let generate() = \"type yes<'a> = { x:int; y: 'a }\" \" "
open MixinProvider

//type MiniMixins = mixin_gen< "TestMetaprograms\\MiniMixins.fsx", metaprogramParameters = "@\"C:\Users\Ross\Documents\MixinProvider\src\MixinProvider\TestMetaprograms\"", generationMode = MixinCompiler.GenerationMode.Namespace >
[<Literal>]
let woah = """let generate() = "type syes<'a> = { x:int; y: 'a }" """
[<Literal>]
let woahh = "let generate() = \"let [<Literal>]program = \"let generate() = \"type yes<'a> = { x:int; y: 'a }\" \" "

let hello = ""
type WTF2 = mixin_ctfe< woah, compileMode=MixinCompiler.CompileMode.CompileWhenMissisng>

//type Full_Test = mixin_full<woah>
//let a = { x = 5; y = true}
//let b = { x = 5; y = 42}



//type FirstTest = mixin_gen< """let generate() = "[<Literal>]let x = 42" """ , compileMode=MixinCompiler.CompileMode.CompileWhenMissisng>

// generates a x = 42
//type Basic_Test = mixin_ctfe< "TestMetaprograms/basic.fsx", outputLocation = @"../bin/" >

//open MixinProvider.Injection
// This is RECURSIVE! The DSL metaprogram also references
// the mixin provider and in turn uses the basic metaprogram 
// to determine one of its values! - head explode!
// type DSL_Test = mixin_gen< "TestMetaprograms\\DSL.fsx" >





//// generates a x = 25  (5 + 20!)
//type Test_Params = mixin_ctfe< "TestMetaprograms/basic_params.fsx", metaprogramParameters = "5 20" >
////
////
//type ConnectionString_Test = mixin_ctfe<"TestMetaprograms/connectionstring.fsx", metaprogramParameters = "\"Dave\"" >
//
//ConnectionString_Test.

//type Excel_Test = mixin_gen< "TestMetaprograms\\excel.fsx" >


//type Test_Inject = mixin_inject< """let mixin_inject injected = "[<Literal>]let x = 42" """, @"F:\GIT\MixinProvider\tests", ProjectSeekMode.Scan >

//
//let _ = mixin_inject1 "" ""
//[<Literal>]let x = 42
//
//
//
//
//
//let _ = mixin_inject_end()
//
//
//
//
//let juan = ""
//
