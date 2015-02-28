[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/issue)](http://issuestats.com/github/pezipink/MixinProvider)
[![Issue Stats](http://issuestats.com/github/pezipink/MixinProvider/badge/pr)](http://issuestats.com/github/pezipink/MixinProvider)

# MixinProvider
The Mixin Provider is essentially a very powerful code generator that executes an F# metaprogram at compile time, compiles the resulting program to an assembly and injects the result back through the type provider.  In a way, this is not a type provider at all, though in "lite" mode it will give you limited access to types - very similar to generative type providers - via the type injection eluded to above. However, that is just a nice bonus, you should view this as a fully powered code generator that leverages the fact that type providers are compiler extensions, in order to hook the generation process directly into the normal F# compilation process.

There are two main modes for use.

###Mixin "Lite"
This usage mode is called "lite" because it suffers from some of the same problems as a traditional F# generative type provider - nameley, you cannot use any F# speical types such as records, DUs, type providers and such directly.  However, you are stil able to generate any .NET or F# code at all, it's just you will only be presented them as normal .NET types.  

You use mxins in this mode by simply aliasing the type provider and metaprogram, and then in the same program you access the types it has generated via the type provider, just like a normal type provider.

* You can execute a arbitarily powerful metaprogram and get striaght at the results from the type provider just like in a generative type provider
* No provided types API, your program simply generates the F# code you require.  There is a compositional code generation DSL to help you with this
* Although you don't get the special F# type treatment, you can still generate a load more stuff than generative type providers, including generic types. 


###Mixin Full
This usage mode is really no different from lite, it is only logistical; Think of this as a seperate code generating machine from your consumning programs.  To use in this manner you dedicate a project to the mixin provider and it's various metaprograms.  You are able to customise the location the generated assemblies will be output, and your consuming projects simply refererence the output in a shared location.  So long as your build order is setup for the code generator projec to run first, you will not notice any difference.

Referenced assemblies of course suffer from no limitations at all.  Using the code generation DSL, a quotation compiler, or some other method of your choosing, you can now generate any and all F# code including Record Types, Discriminated Unions, Extension Methods, Generics, Computation Expressions and even other type providers

###Other features

* The metaprograms used to generate code are practically all-powerful.  You can use type providers within metaprograms to load your metadata for generating types; you can even use the Mixin type provider inside other metaprograms recursively!
* The mixin provider is also very complimentary to other type providers.  If you are in the frustrating situation of using a type provider that requires a literal static parameter, and you have the information at compile time to calcuate that parameter, you can easily inline a mixin-lite metaprogram to generate a literal for you and feed it straight into the other type provider! 


###Notes
This project is extemely alpha, it is still missing a bunch of features.  It also jumps through lots of hoops and breaks probably quite a lot of rules :)  Use with caution!

Note there is no scope for erased types here, this is purely to fill a gap where F# has no story - code generation.  In fact, the provided types API is not used at all in this type provider. 

Documentation: http://pezipink.github.io/MixinProvider

## Maintainer(s)

- [@pezipink](https://github.com/pezipink)



