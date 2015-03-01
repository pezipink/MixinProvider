// this metaprogram demonstrates how it is possible to accept 
// parameters into the generate function, from the static parameters
// of the mixin provider
let generate x y  = 
    let z = x + y
    sprintf "[<Literal>]let x = %i" z
