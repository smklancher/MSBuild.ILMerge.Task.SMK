## MSBuild.ILMerge.Task
Forked from https://dev.azure.com/Refactorius/MsBuild.ILMerge.Task 2021-03-07.
Version on nuget and even with changes since that release was not merging PackageReference packages in SDK style projects.  Made changes here so that they are merged, and definitely works for my current project now.  Unsure if the changes cover all cases though.  Ideally the original package gets updated eventually and I can abandon this one.

Also fixed after build command to pack the nuget package.

- Build project to product nuget package, move elsewhere if needed.
- Add nuget.config to solution folder with folder updated to where you will keep the nuget package.
- Update project to new reference

nuget.config:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <add key="MyLocalSharedSource" value="..\..\..\some\folder" />
    </packageSources>
</configuration>
```

## Original - MSBuild.ILMerge.Task
**MSBuild ILMerge task** allows you to use the [famous ILMerge utility](http://research.microsoft.com/en-us/people/mbarnett/ILMerge.aspx) in automated builds and/or Visual Studio 2013/2015/2017 projects.

It is similar to the ILMerge.MSBuild.Tasks nuget package, originally published by neoeinstein@gmail.com (that seems to be abandoned) with the following additional features: 

- It supports all the latest ILMerge options;
- ILMerge.exe is discovered as package dependency;
- The .targets file overrides the CopyFilesToOutputDirectory build target, leaving the AfterBuild target free for further customizaton;
- It attempts to guess the reasonable merge order for the included assemblies and lets you to finetune it with a configuration file.

**ILMerge.exe** is not included into this package because its license prohibits redistribution. Referencing it as package dependency and loading it from its package location ensures that the latest ILMerge version is always used.

You can install this task as a [NuGet package](https://www.nuget.org/packages/MSBuild.ILMerge.Task/).