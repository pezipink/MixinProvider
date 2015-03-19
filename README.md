[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/issue)](http://issuestats.com/github/pezipink/MixinProvider)
[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/pr)](http://issuestats.com/github/pezipink/MixinProvider)

# MixinProvider

You can read an introduction post on my blog here http://pinksquirrellabs.com/post/2015/03/01/Introducing-The-Mixin-Type-Provider.aspx

The Mixin Provider is essentially a very powerful code generator - it evaluates an F# metaprogram at compile time, and compiles the resulting program to an assembly - or it can inject code into existing F# source files within your projects by using a combination of special dummy injection functions and a metaprogram.

In a way, this is not a type provider at all, though in "lite" mode it will give you limited access to types - very similar to generative type providers. However, that is just a nice bonus, you should view this as a fully powered code generator that leverages the fact that type providers are compiler extensions, in order to hook the generation process directly into the normal F# compilation process.

There are three main modes for use.

###Mixin Lite
This usage mode is called "lite" because it suffers from some of the same problems as a traditional F# generative type provider - namely, you cannot use any F# speical types such as records, DUs and type providers.  However, you are stil able to generate any F# code, it's just you will only be presented with them as normal .NET types.  

You use mxins in this mode by simply aliasing the type provider and metaprogram, and then in the same program you access the types it has generated via the type provider, exactly like a normal type provider.

* You can execute a arbitarily powerful metaprogram and get straight at the results from the type provider exactly like a generative type provider
* No provided types API, your program simply generates the F# code you require.  There is a compositional code generation DSL to help you with this
* Although you don't get the special F# type treatment, you can still generate a load more stuff than generative type providers, including generic types. 


###Mixin Full
This usage mode is really no different from lite, it is only logistical; Think of this as a seperate code generating machine from your consuming programs.  To use in this manner you dedicate a project to the mixin provider and its various metaprograms.  You are able to customise the location that the generated assemblies will be output, and your consuming projects simply refererence the output in a shared location.  So long as your build order is setup for the code generator projec to run first, you will not notice any difference.

Referenced assemblies of course suffer from no limitations at all.  Using the code generation DSL, a quotation compiler, or some other method of your choosing, you can now generate any and all F# code including Record Types, Discriminated Unions, Extension Methods, Generics, Computation Expressions and even other type providers

### Mixin Inject
This mode allows you to inject F# code into another project by  calling dummy injection functions.  At compile time the Mixin provider will scan for F# projects, find locations where injection functions are used, call the equivalent functions in the metaprogram and directly inject the resulting code into those locations at the correct indentation level.  (WORK IN PROGRESS, NOT YET SUPPORTED!)

### Metaprograms
A metaprogram is any .fsx file or equivalent string.  You can do anything and everything you want in your metaprogram. For Lite and Full modes, the only rule is that you have one root level function called "generate" that should return the program to compile. This function may accept parameters which you are able to supply via the type procider's static parameters

In the case of injection mode, your metaprogram should have an implementation for each dummy injection function you use in the target F# projects.

### SquirrelMix
"A cement for woodland creatures" ™
SquirrelMix is a heavily compositional domain-specfic langauge for generating F# code in an idiomatic way.  The library of functions can create most common F# code and will automatically take care of troublesome problems such as ensuring the correct identation level for the code.

###Other features

* The metaprograms used to generate code are practically all-powerful.  You can use type providers within metaprograms to load your metadata for generating types; you can even use the Mixin type provider inside other metaprograms recursively!
* The mixin provider is also very complimentary to other type providers.  If you are in the frustrating situation of using a type provider that requires a literal static parameter, and you have the information at compile time to calcuate that parameter, you can easily inline a mixin-lite metaprogram to generate a literal for you and feed it straight into the other type provider! 
* There are various switches to change the location where things are read from and written too, you can also tell the mixin provider to always or compile when the assembly is either missing or the static parameters changed.


###Notes
This project is extemely alpha, it is still missing a bunch of features.  It also jumps through lots of hoops and breaks probably quite a lot of rules :)  Use with caution!

Note there is no scope for erased types here, this is purely to fill a gap where F# has no story - code generation.  In fact, the provided types API is not used at all in this type provider. 

### Stuff to do
Here are some of the outstanding things: 

* Debug and release modes, plus the outputting of debug symbols
* Better diagnostics and debugging capabilities

Documentation: http://pezipink.github.io/MixinProvider

## Maintainer(s)

- [@pezipink](https://github.com/pezipink)



