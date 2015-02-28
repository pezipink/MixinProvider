// Learn more about F# at http://fsharp.org. See the 'F# Tutorial' project
// for more guidance on F# programming.

//#r "..\..\"
//#load "MixinProvider.fs"
//open MixinProvider

//let num = Library.hello 42
//printfn "%i" num

open System
open System.IO
let pf = System.Environment.ExpandEnvironmentVariables("%programfiles%")

let frameworkReferenceLocation = Path.Combine(pf,@"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1")

let fsharpCoreLocation = Path.Combine(pf, @"Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.3.1.0\FSharp.Core.dll")

let defaultReferenceAssemblies = [
    @"mscorlib.dll"
    @"System.Core.dll"
    @"System.dll"
    @"System.Numerics.dll"
    @"Facades\System.Collections.Concurrent.dll"
    @"Facades\System.Collections.dll"
    @"Facades\System.ComponentModel.Annotations.dll"
    @"Facades\System.ComponentModel.dll"
    @"Facades\System.ComponentModel.EventBasedAsync.dll"
    @"Facades\System.Diagnostics.Contracts.dll"
    @"Facades\System.Diagnostics.Debug.dll"
    @"Facades\System.Diagnostics.Tools.dll"
    @"Facades\System.Diagnostics.Tracing.dll"
    @"Facades\System.Dynamic.Runtime.dll"
    @"Facades\System.Globalization.dll"
    @"Facades\System.IO.dll"
    @"Facades\System.Linq.dll"
    @"Facades\System.Linq.Expressions.dll"
    @"Facades\System.Linq.Parallel.dll"
    @"Facades\System.Linq.Queryable.dll"
    @"Facades\System.Net.NetworkInformation.dll"
    @"Facades\System.Net.Primitives.dll"
    @"Facades\System.Net.Requests.dll"
    @"Facades\System.ObjectModel.dll"
    @"Facades\System.Reflection.dll"
    @"Facades\System.Reflection.Emit.dll"
    @"Facades\System.Reflection.Emit.ILGeneration.dll"
    @"Facades\System.Reflection.Emit.Lightweight.dll"
    @"Facades\System.Reflection.Extensions.dll"
    @"Facades\System.Reflection.Primitives.dll"
    @"Facades\System.Resources.ResourceManager.dll"
    @"Facades\System.Runtime.dll"
    @"Facades\System.Runtime.Extensions.dll"
    @"Facades\System.Runtime.InteropServices.dll"
    @"Facades\System.Runtime.InteropServices.WindowsRuntime.dll"
    @"Facades\System.Runtime.Numerics.dll"
    @"Facades\System.Runtime.Serialization.Json.dll"
    @"Facades\System.Runtime.Serialization.Primitives.dll"
    @"Facades\System.Runtime.Serialization.Xml.dll"
    @"Facades\System.Security.Principal.dll"
    @"Facades\System.ServiceModel.Duplex.dll"
    @"Facades\System.ServiceModel.Http.dll"
    @"Facades\System.ServiceModel.NetTcp.dll"
    @"Facades\System.ServiceModel.Primitives.dll"
    @"Facades\System.ServiceModel.Security.dll"
    @"Facades\System.Text.Encoding.dll"
    @"Facades\System.Text.Encoding.Extensions.dll"
    @"Facades\System.Text.RegularExpressions.dll"
    @"Facades\System.Threading.dll"
    @"Facades\System.Threading.Tasks.dll"
    @"Facades\System.Threading.Tasks.Parallel.dll"
    @"Facades\System.Threading.Timer.dll"
    @"Facades\System.Xml.ReaderWriter.dll"
    @"Facades\System.Xml.XDocument.dll"
    @"Facades\System.Xml.XmlSerializer.dll"]


let referenceArguments =
    seq { yield fsharpCoreLocation
          yield! defaultReferenceAssemblies 
                 |> List.map(fun l -> 
                    sprintf "-r:\"%s\""
                        (Path.Combine(frameworkReferenceLocation,l))) }    
    |> Seq.toList

referenceArguments |> List.map (sprintf "%A")
referenceArguments |> List.map(fun l -> sprintf "%A"(File.Exists(l)))

System.IO.Path.GetFullPath(pf)
let di = System.IO.DirectoryInfo("%programfiles%")


System.IO.File.Exists(@"%programfiles%\reference assemblies\Microsoft\Framework\.NETFramework\v4.5.1\mscorlib.dll")
