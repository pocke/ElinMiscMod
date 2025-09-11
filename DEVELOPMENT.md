## Build

First, put `Directory.Build.props` with the following content in the root directory of the project.
Change the `ElinGamePath` and `WorkshopPath` to the path of your Elin installation.

```xml
<Project>
  <PropertyGroup>
    <ElinGamePath>C:\Program Files (x86)\Steam\steamapps\common\Elin</ElinGamePath>
  </PropertyGroup>
  <PropertyGroup>
    <WorkshopPath>C:\Program Files (x86)\Steam\steamapps\workshop\content</WorkshopPath>
  </PropertyGroup>
</Project>
```

After that, subscribe [YKFramework](https://steamcommunity.com/sharedfiles/filedetails/?id=3400020753) mod on Steam.

Then, run the following command to build the project.

```console
dotnet build
```
