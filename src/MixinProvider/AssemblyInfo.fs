namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("MixinProvider")>]
[<assembly: AssemblyProductAttribute("MixinProvider")>]
[<assembly: AssemblyDescriptionAttribute("Type provider for generating F# code via compile time metaprograms")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
