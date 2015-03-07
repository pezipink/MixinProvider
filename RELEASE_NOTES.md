#### 0.0.2-alpha - 07.03.2015 
* Change provider to make it inheritable, so you can create specialized versions of it
* Lots of changes and fixes to relative path resolution in all areas
* Changed available compile modes
* Added debug symbols to output (all the time at the moment)
* You can now choose if you want the wrapper type to be a namespace, AutoOpenModule or Module.  This is mainly to cater for  generating type providers.
* Some tidy up, more tests
* A new example that uses the F# checker and source analysis to generate specialized mixin type providers for each metaprogram with the correct static parameters that the generate function expects
* MIXIN directive now passed to FSI and FSC, this allows you to have different resolution paths for FSI and gives you a way to specifiy how to resolve and add -r parameters to FSC
* New SquirrelMix functions 
* Other stuff

#### 0.0.1-alpha - 28.02.2015 
* Initial rough and very incomplete alpha release of MixinProvider