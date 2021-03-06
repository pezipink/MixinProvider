﻿// Ross McKinlay, 2015
// this file contains a domain specific compositional language 
// for generating F# code with the minimum of concerns
namespace MixinProvider

[<AutoOpen>]
module SquirrelMix =
    open System
    open System.Text
    type sb = StringBuilder

    let mapPipe f = Seq.map f >> Seq.reduce (>>)

    /// appends count number of spaces
    let spaces count (sb:sb) = 
        let spaces = String(' ',count)
        sb.Append spaces

    /// appends any text
    let ap (text:string) (sb:sb) = sb.Append text

    /// appends an escaped, quoted string from a string
    let str (text:string) = sprintf "\"%s\""text  //todo : escaping

    let stra (text:string) (sb:sb) = sb.AppendFormat("\"{0}\"", text) //todo : escaping

    let space = spaces 1

    let indent count = spaces (count*4)

    let api text indentLevel  = indent indentLevel >> ap text

    

    /// appends a newline character
    let newl (sb:sb) = sb.AppendLine() 

    /// appends a new line and then the specified indent level
    /// this function is not usuallly very useful, though as it
    /// takes the additional parameter it can be used in places expecting
    /// int -> sb  insted of having to write (fun _ -> newl)
    let newli indentLevel (sb:sb) = sb.AppendLine() |>  indent indentLevel 

    /// appends an xml comment at the given indent level
    let xmlComment comment indentLevel = indent indentLevel >> ap(sprintf "///%s" comment) >> newl  

    /// wraps text in parenthesis and returns the string
    let wrapParens (text:string) = sprintf "(%s)" text    

    /// wraps text in braces and returns the string
    let wrapBraces (text:string) = sprintf "{ %s }" text

    /// annotates a parameter in the format 'name : type' and returns the string
    let annoParam (pName:string) (pType:string) = sprintf "%s : %s" pName pType

    /// annotates a parameter in the format '(name : type)' and returns the string
    let annoParamParens (pName:string) (pType:string) = sprintf "(%s : %s)" pName pType

    /// joins a sequence of strings with the given seperator
    let join (seperator:string) (values:seq<string>) = String.Join(seperator,values)

    type MemberType =
         | Instance of identifier : string 
         | InstanceOverride of identifier : string
         | Static
         | StaticOverride
         
    type ArgsType =
        | Partial of (string * string option) list
        | Tuple of (string * string option) list
        | NoArgs  

    /// creates a member defintion at the given indent level
    /// memberType - static, or instance with a self identifier value
    /// qualifer - any qualifiying string eg private
    /// name - the name of the member
    /// args - list of argument names and optionally types, denoted as partial style
    ///        or tuples. 
    ///        eg; Partial [("x",Some "int"),("y",None)] evaluates as
    ///              foo (x:int) y = ...
    ///        and Tuple [("x",Some "int"),("y",None)] evaluates as
    ///              foo (x:int,y) = ...
    /// impl - a function that will be called with (indentLevel+1) to create
    ///        the body of the member
    /// TODO - generic arguments
    let cmember memberType qualifier name args impl indentLevel =
        let stat = memberType  |> function Instance _         | InstanceOverride _  -> ""          | _ -> "static "
        let ident = memberType |> function Instance id        | InstanceOverride id -> id          | _ -> ""
        let mem = memberType   |> function InstanceOverride _ | StaticOverride      -> "override " | _ -> "member "
        let signature = 
            match args with 
            | Partial args ->
                let cp = function n, None -> n | n, Some t -> annoParamParens n t 
                join " " (Seq.map cp args)
            | Tuple args -> 
                let cp = function n, None -> n | n, Some t -> annoParam n t             
                wrapParens (join ", " (Seq.map cp args))
            | NoArgs ->"()" 
        indent indentLevel
        >> ap stat >> ap qualifier >> space >> ap mem >> ap ident >> ap "." >> ap name >> ap signature >> ap " = " >> newl
        >> (impl (indentLevel + 1))
  
    /// appends "with.." at the indent level then calls the impl function with
    /// indentLevel+1 (this function should be formed from a composition of 
    /// cmember, cinterface etc)
    let cwithMembers impl indentLevel =
        if Seq.isEmpty impl then id else 
        indent indentLevel >> ap "with" >> newl 
        >> mapPipe(fun f -> f (indentLevel+1) >> newl) impl

    /// todo:generics
    let ctype name args inheritFrom members indentLevel =
        indent indentLevel
        >> ap "type " >> ap name 
        >> ap(wrapParens (join "; " (Seq.map(fun (p,t) -> annoParam p t) args))) >> ap " =" >> newl
        >> (if inheritFrom = "" then id else indent (indentLevel+1) >> ap "inherit " >> ap inheritFrom) >> newl
        >> cwithMembers members (indentLevel+1)
    
    let carray members indentLevel =
        indent indentLevel
        >> ap "[|" >> newl
        >> (if Seq.isEmpty members then id else mapPipe(fun f -> f (indentLevel+1) >> ap ";" >> newl) members)
        >> indent indentLevel >> ap "|]" >> newl
        
    ///creates a record type at indent level with the specified fields
    ///and optionally a function list with member / interface implementations
    let crecord name fields members indentLevel =
        indent indentLevel 
        >> ap "type " >> ap name >> space >> ap " = "
        >> ap(wrapBraces (join "; " (Seq.map(fun (p,t) -> annoParam p t) fields))) >> newl
        >> cwithMembers members indentLevel

    /// instantiates a record type in the format  { f = v; f2 = v2 }
    /// ignores indent level
    let irecord fields = 
        fields
        |> Seq.map(fun (f,v) -> sprintf "%s = %s; " f v) 
        |> join ""
        |> wrapBraces
        |> ap
        
    /// creates a single union type case with any amount of type arguments
    /// that can optionally be named (F# 4)
    /// type args should be in the form (name,type name) where name can be blank
    let cunionTypeCase name args =
        sprintf "| %s of %s" name 
            (join " * " (Seq.map(fun (identifier,typeName) ->
                                    if String.IsNullOrWhiteSpace identifier then typeName
                                    else sprintf "%s : %s" identifier typeName)
                                    args))

    let cunionType name cases members indentLevel =
        indent indentLevel 
        >> ap "type " >> ap name >> space >> ap " = " >> newl
        >> mapPipe (fun (name,args) -> indent (indentLevel+1) >> ap (cunionTypeCase name args) >> newl) cases
        >> cwithMembers members indentLevel

    let cmatchExpr pattern guard indentLevel impl =
        let guard = guard |> function None -> "" | Some g -> sprintf " when %s" g
        indent indentLevel  
        >> ap "| " >> ap pattern >> ap guard >> impl

    /// writes a match expresssion at indentLevel in the format 
    /// match expr with ..
    /// and then calls cmatchExpr on each case at indentLevel, 
    /// applying (indentlevel+1) and a new line to each case function
    let cmatch expr cases indentLevel =
        indent indentLevel
        >> ap "match " >> ap expr >> ap " with" >> newl
        >> mapPipe (fun (expr,guard,impl) -> 
                        
                         (cmatchExpr expr guard indentLevel  (impl (indentLevel+1))) >> newl) cases

    let cextension typeName members indentLevel =
        indent indentLevel
        >> ap "type " >> ap typeName >> newl
        >> cwithMembers members indentLevel

    /// writes a let binding at the indent level, and calls
    /// (impl indentLevel+1) on a new line to write the implentation
    /// this is useful for let bindings that do not fit on the same line
    /// eg
    /// let x =
    ///  doSomeStuff "stuff"
    ///  |> moreStuf
    let cleti identifier impl indentLevel =
        indent indentLevel
        >> ap "let " >> ap identifier >> ap " = " >> newl >> impl (indentLevel+1)

    /// writes a let binding at the indent level, expecting that
    /// the impl function is a one liner that does not expect an 
    /// indent level parameter.  
    /// eg
    /// let x = List.reduce(+) input
    let clet identifier impl indentLevel =
        indent indentLevel
        >> ap "let " >> ap identifier >> ap " = " >> impl 
    
    /// takes a list of functions, applies indentLevel >> newl to each, 
    /// then composes the whole lot into one mega function.  Use this function
    /// whenever you want to use a bunch of the other functions in one indent block,
    /// eg when implementing functions / members.
    let ccode impl indentLevel = mapPipe(fun f -> f indentLevel >> newl) impl

    /// creates module with the given name, the list of 
    /// implementation functions have indentLevel+1 applied 
    /// to them and are then composed into one function
    let cmodule name impl indentLevel =
        indent indentLevel
        >> ap "module " >> ap name  >> ap " =" >> newl
        >> mapPipe(fun f -> f (indentLevel+1) >> newl) impl

    /// creates a pipeline of function calls starting with
    /// the start string
    /// eg; pipeline "names" []
    let cpipeline start impl indentLevel =
        ap start >> (newli indentLevel)
        >> mapPipe(fun f -> ap "|>" >> f (indentLevel+1) >> newl) impl

    let clambda args impl indentLevel =
        ap "fun " >> args >> ap " -> " >> impl (indentLevel+1)

    /// this function is used to insert the equivalent of #I and #r directives
    /// at the top of your file.  Whilst the resulting program is not a 
    /// interactive file, the mixin compiler will extract these, resolve, and 
    /// pass them along to the fsc as -r arguments. The #if MIXIN is
    /// just a dummy so the compiler ignores this block
    let genReferences locations references indentLevel =
        let r loc = sprintf "#r @\"%s\"" loc
        let i loc = sprintf "#I @\"%s\"" loc
        ap "#if MIXIN  \r\n" 
        >> ap (join "\r\n" (Seq.map i locations)) 
        >> newl
        >> ap (join "\r\n" (Seq.map r references)) 
        >> newl
        >> ap "#endif\r\n"
        
    let copen opens indentLevel =        
        ap (join "\r\n" (Seq.map (sprintf "open %s") opens)) >> newl


    /// creates an if .. then .. else expression, the functions 
    /// passed for to create the branch implementations are 
    /// always applied with indentLevel+1
    /// eg
    /// if x = 1 then
    ///     true
    /// else
    ///     false
    let cifThenElse pred trueExpr falseExpr indentLevel =
        indent indentLevel
        >> ap "if " >> pred >> ap " then" >> newl
        >> trueExpr(indentLevel+1)
        >> indent indentLevel
        >> ap "else" >> newl
        >> falseExpr(indentLevel+1)
