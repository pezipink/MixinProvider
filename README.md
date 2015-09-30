[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/issue)](http://issuestats.com/github/pezipink/MixinProvider)
[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/pr)](http://issuestats.com/github/pezipink/MixinProvider)

# MixinProvider

You can read an introduction post on my blog here http://pinksquirrellabs.com/post/2015/03/01/Introducing-The-Mixin-Type-Provider.aspx  (NOTE! this post is slightly out of date now, hopefully I will write another one soon...)

The Mixin Provider is essentially a very powerful code generator - it evaluates an F# metaprogram at compile time, and compiles the resulting program to an assembly - or it can inject code into existing F# source files within your projects by using a combination of special dummy injection functions and a metaprogram.

In a way, this is not a type provider at all, though in CTFE mode it will give you limited access to types - very similar to generative type providers. However, that is just a nice bonus, you should view this as a fully powered code generator that leverages the fact that type providers are compiler extensions, in order to hook the generation process directly into the normal F# compilation process.

You can watch a intro talk recorded at the 2015 F# eXchange here https://skillsmatter.com/skillscasts/6159-meta-programming-madness-with-the-mixin-type-provider

There are three main modes for use.

###Mixin CTFE
This mode of operation is largely aimed at an odd form of "Compile Time Function Execution" where you execute a metaprogram to crunch some numbers and then produce a result which you will typically expose as a value on a module.  You a can also generate functions and normal types that do not use generics in their signatures and are not special F# types.

This mode is the most similar to a traditional F# type provider.  Simply alias the `mixin_ctfe` type and pass it a metaprgoram as a static parameter, and then in the same program you can access the data it has generated exactly like a normal type provider.

* You can execute a arbitrarily powerful metaprogram and get straight at the results from the type provider exactly like a generative type provider
* No provided types API, your program simply generates the F# code you require.  There is a compositional code generation DSL to help you with this
* This mode is particularly useful for generating [<Literal>] values at compile time to pass as static parameters to other F# type providers, and pre-calculating lookup tables or other heavy operations that are able to be executed at compile time.
* If you throw an exception in the metaprogram, the compilation will fail - this gives you a kind of compile-time assertion which can be useful in a variety of situations.

###Mixin Full
This usage mode is only marginally different from CTFE, and mostly logistical; Think of this as a separate code generating machine from your consuming programs.  To use in this manner you dedicate a project to the mixin provider and its various metaprograms.  You are able to customise the location that the generated assemblies will be output, and your consuming projects simply reference the output in a shared location.  So long as your build order is setup for the code generator project to run first, you will not notice any difference.

Referenced assemblies of course suffer from no limitations at all.  Using the code generation DSL, a quotation compiler, or some other method of your choosing, you can now generate any and all F# code including Record Types, Discriminated Unions, Extension Methods, Generics, Computation Expressions and even other Type Providers!

In your code-generation project, reference the `mixin_full` type and pass your metaprogram as a static parameter.  There are additional static parameters to control the location of the output assembly and other things.  The name of your type alias will become the name of the generated assembly.  In your consuming project, reference the resulting assembly like normal, and adjust your build dependencies accordingly.

### Mixin Inject
(WORK IN PROGRESS, NOT YET SUPPORTED!)
This mode allows you to inject F# code into another project by calling dummy injection functions or using attributes.  At compile time the Mixin provider will scan for F# projects, find locations where injection functions are used, call the equivalent functions in the metaprogram and directly inject the resulting code into those locations at the correct indentation level.  

### Metaprograms
A metaprogram is any .fsx file or equivalent string.  You can do anything and everything you want in your metaprogram. For Lite and Full modes, the only rule is that you have one root level function called "generate" that should return the program to compile. This function may accept parameters which you are able to supply via the type procider's static parameters

In the case of injection mode, your metaprogram should have an implementation for each dummy injection function you use in the target F# projects.

### SquirrelMix
"A cement for woodland creatures" ™

SquirrelMix is a heavily compositional domain-specfic langauge for generating F# code in an idiomatic way.  The library of functions can create most common F# code and will automatically take care of troublesome problems such as ensuring the correct identation level for the code.

Note this DSL is far from complete and not that well tested, feel free to test / fix / extend and send your pull requests :)

###Other features

* The metaprograms used to generate code are practically all-powerful.  You can use type providers within metaprograms to load your metadata for generating types; you can even use the Mixin type provider inside other metaprograms recursively!
* The mixin provider is also very complimentary to other type providers.  If you are in the frustrating situation of using a type provider that requires a literal static parameter, and you have the information at compile time to calculate that parameter, you can easily inline a CTFE metaprogram to generate a literal for you and feed it straight into the other type provider! 
* There are various switches to change the location where things are read from and written too, you can also tell the mixin provider to always or compile when the assembly is either missing or the static parameters changed.


###Notes
This project is extremely alpha, it is still missing a bunch of features.  It also jumps through lots of hoops and breaks probably quite a lot of rules :)  Use with caution!

Note there is no scope for erased types here, this is purely to fill a gap where F# has no story - code generation.  In fact, the provided types API is not used at all in this type provider. 

### Stuff to do
Here are some of the outstanding things: 

* Debug and release modes, plus the outputting of debug symbols
* Better diagnostics and debugging capabilities

Documentation: http://pezipink.github.io/MixinProvider

## Maintainer(s)

- [@pezipink](https://github.com/pezipink)



