#r @"F:\GIT\SqlProvider\bin\FSharp.Data.SqlProvider.dll" 
#load "F:\GIT\MixinProvider\src\MixinProvider\SquirrelGen.fs"


[<Literal>] 
let peopleCs = @"Driver={Microsoft Excel Driver (*.xls)};DriverId=790;Dbq=I:\people.xls;DefaultDir=I:\;"

open FSharp.Data.Sql
open MixinProvider
open System.Text

type sql = SqlDataProvider<Common.DatabaseProviderTypes.ODBC, peopleCs>
let ctx = sql.GetDataContext()

let generate() =
    // create a person record type
    let personType = 
        crecord
            "Person"
            [ "firstName", "string"
              "lastName", "string"
              "age", "int"  ] []

    let createPersonRecord firstName lastName age =
        let fullName = sprintf "%s%s" firstName lastName
        
        // create record instantiation
        let record = 
            instRecord
               ["firstName", str firstName
                "lastName", str lastName
                "age", age            ]
        
        // create let expression    
        clet fullName record

    let peopleRecords =
        ctx.``[].[SHEET1$]``
        |> Seq.map(fun person -> 
            // generate people from the spreadsheet
            createPersonRecord 
                person.FIRSTNAME 
                person.LASTNAME 
                (string person.AGE))
        |> Seq.toList

    (1,new StringBuilder()) 
    // create a module with all our stuff in it
    ||> cmodule "People" (personType :: peopleRecords)
    |> fun sb -> sb.ToString()
