open System
let generate user =
    match user with 
    | "John" when System.DateTime.Now.DayOfWeek = DayOfWeek.Monday ->
        "let [<Literal>] connectionString = \"JohnMon!\" "
    | "John" | "Dave" -> 
        "let [<Literal>] connectionString = \"normal :(\" "
    | _ -> failwithf "user %s is not allowed a connection string!" user