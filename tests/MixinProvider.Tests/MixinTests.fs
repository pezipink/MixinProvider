module MixinProvider.Mixin.Tests


open NUnit.Framework
open System.Text
open System.Diagnostics


open MixinProvider

//type FirstTest = mixin_gen< """let generate() = "let x = 42" """ >

// generates a x = 42
type Basic_Test = mixin_gen< "TestMetaprograms\\basic.fsx", outputLocation = @"..\bin\" >


// This is RECURSIVE! The DSL metaprogram also references
// the mixin provider and in turn uses the basic metaprogram 
// to determine one of its values! - head explode!
// type DSL_Test = mixin_gen< "TestMetaprograms\\DSL.fsx" >


// generates a x = 25  (5 + 20!)
//type Test_Params = mixin_gen< "TestMetaprograms\\basic_params.fsx", metaprogramParameters = "5 20" >


//type ConnectionString_Test = mixin_gen<"TestMetaprograms\\connectionstring.fsx", metaprogramParameters = "\"John\"" >

//type Excel_Test = mixin_gen< "TestMetaprograms\\excel.fsx" >