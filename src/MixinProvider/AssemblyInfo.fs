namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("MixinProvider")>]
[<assembly: AssemblyProductAttribute("MixinProvider")>]
[<assembly: AssemblyDescriptionAttribute("Type provider for generating F# code via compile time metaprograms")>]
[<assembly: AssemblyVersionAttribute("0.0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.2"
