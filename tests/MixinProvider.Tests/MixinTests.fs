module MixinProvider.Mixin.Tests

open MixinProvider
open NUnit.Framework
open System.Text
open System.Diagnostics


// generates a x = 42
type Test2 = mixin_gen< "TestMetaprograms\\basic.fsx" >


// This is RECURSIVE! The DSL metaprogram also references
// the mixin provider and in turn uses the basic metaprogram 
// to determine one of its values! - head explode!
type DSL_Test = mixin_gen< "TestMetaprograms\\DSL.fsx" >


