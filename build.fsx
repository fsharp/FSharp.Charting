// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I "tools/FAKE/tools"
#r "tools/FAKE/tools/FakeLib.dll"

open System
open System.IO
open System.Text.RegularExpressions
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let files includes = 
  { BaseDirectory = __SOURCE_DIRECTORY__
    Includes = includes
    Excludes = [] } 

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Charting"
let authors = ["Carl Nolan, Tomas Petricek"]
let summary = "A Charting Library for F#"
let description = """
  The F# Charting library (FSharp.Charting.dll) is a compositional library for creating charts
  from F#. It is designed to be a great fit for data scripting in F# Interactive, but 
  charts can also be embedded in Windows applications. The library is a wrapper for .NET Chart
  Controls, which are only supported on Windows."""
let tags = "F# FSharpChart charting plotting visualization"

// Information for the ASP.NET build of the project 
let projectAspNet = "FSharp.Charting.AspNet"
let summaryAspNet = summary + " (ASP.NET web forms)"
let tagsAspNet = tags + " ASPNET"
let descriptionAspNet = """
  The F# Charting library (FSharp.Charting.AspNet.dll) is an ASP.NET Web Forms build
  of FSharp.Charting. It is experimental."""

// Information for the Gtk build of the project 
let projectGtk = "FSharp.Charting.Gtk"
let summaryGtk = summary + " (Gtk, cross-platform)"
let tagsGtk = tags + " Gtk GtkSharp OxyPlot"
let descriptionGtk = """
  The F# Charting library (FSharp.Charting.Gtk.dll) is a cross-platform variation of
  of FSharp.Charting. It can be used on Windows, OSX and other platforms supporting Gtk."""

// Read additional information from the release notes document
let releaseNotes, version = 
    let lastItem = File.ReadLines "RELEASE_NOTES.md" |> Seq.last
    let firstDash = lastItem.IndexOf('-')
    ( lastItem.Substring(firstDash + 1 ).Trim(), 
      lastItem.Substring(0, firstDash).Trim([|'*'|]).Trim() )

let compilingOnUnix =
    match Environment.OSVersion.Platform with
    | PlatformID.MacOSX | PlatformID.Unix -> true
    | _ -> false

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" (fun _ ->
    [ ("src/AssemblyInfo.fs", "FSharp.Charting", project, summary)
      ( "src/AssemblyInfo.AspNet.fs", "FSharp.Charting.AspNet", projectAspNet, summaryAspNet ) 
      ( "src/AssemblyInfo.Gtk.fs", "FSharp.Charting.Gtk", projectGtk, summaryGtk ) ]
    |> Seq.iter (fun (fileName, title, project, summary) ->
        CreateFSharpAssemblyInfo fileName
           [ Attribute.Title title
             Attribute.Product project
             Attribute.Description summary
             Attribute.Version version
             Attribute.FileVersion version] )
)

// --------------------------------------------------------------------------------------
// Update the assembly version numbers in the script file.

Target "UpdateFsxVersions" (fun _ ->
    for nm in [ "FSharp.Charting"; "FSharp.Charting.Gtk" ] do
        for path in [ @"./src/" + nm + ".fsx" ] @ Seq.toList (Directory.EnumerateFiles "examples")  do
          let text1 = File.ReadAllText(path)
          // Adjust entries like #I "../../../packages/FSharp.Charting.0.84"
          let text2 = Regex.Replace(text1, "packages/" + nm + @".(.*)/lib/net40""", "packages/" + nm + sprintf @".%s/lib/net40""" version)
          // Adjust entries like #load "packages/FSharp.Charting.0.84/FSharp.Charting.fsx"
          let text3 = Regex.Replace(text2, "packages/" + nm + @".(.*)/" + nm + ".fsx", "packages/" + nm + sprintf @".%s/" version + nm + ".fsx")
          if text1 <> text3 then 
              File.WriteAllText(path, text3)
)

// --------------------------------------------------------------------------------------
// Restore NuGet packages

Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs ["bin"]
)

// --------------------------------------------------------------------------------------
// Build library (builds Visual Studio solution, which builds multiple versions
// of the runtime library & desktop + Silverlight version of design time library)

Target "Build" (fun _ ->
    (files [if not compilingOnUnix then
                yield "src/FSharp.Charting.fsproj";
                yield "tests/FSharp.Charting.Tests.fsproj"
                yield "src/FSharp.Charting.AspNet.fsproj"
            yield "src/FSharp.Charting.Gtk.fsproj" ])
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)
// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

Target "RunTests" (fun _ ->

    // Will get NUnit.Runner NuGet package if not present
    // (needed to run tests using the 'NUnit' target)
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))

    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion

    ActivateFinalTarget "CloseTestRunner"

    if not compilingOnUnix then
      (files [ "tests/bin/Release/FSharp.Charting.Tests.dll"])
      |> NUnit (fun p ->
        { p with
            ToolPath = nunitPath
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

FinalTarget "CloseTestRunner" (fun _ ->  
    ProcessHelper.killProcess "nunit-agent.exe"
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->

    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let descriptionAspNet = descriptionAspNet.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let descriptionGtk = descriptionAspNet.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let nugetPath = ".nuget/NuGet.exe"

    if not compilingOnUnix then
      NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = version
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] 
            WorkingDir = "./nuget" })
        "nuget/FSharp.Charting.nuspec"

    if not compilingOnUnix then
      NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = projectAspNet
            Summary = summaryAspNet
            Description = descriptionAspNet
            Version = version
            ReleaseNotes = releaseNotes
            Tags = tagsAspNet
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] 
            WorkingDir = "./nuget" })
        "nuget/FSharp.Charting.AspNet.nuspec"

    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = projectGtk
            Summary = summaryGtk
            Description = descriptionGtk
            Version = version
            ReleaseNotes = releaseNotes
            Tags = tagsGtk
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] 
            WorkingDir = "./nuget" })
        "nuget/FSharp.Charting.Gtk.nuspec"

)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "UpdateDocs" (fun _ ->

    executeFSI "tools" "build-docs.fsx" [] |> ignore

    DeleteDir "gh-pages"
    Repository.clone "" "https://github.com/fsharp/FSharp.Charting.git" "gh-pages"
    Branches.checkoutBranch "gh-pages" "gh-pages"
    CopyRecursive "docs" "gh-pages" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "gh-pages" (sprintf """commit -a -m "Update generated documentation for version %s""" version) |> printfn "%s"
    Branches.push "gh-pages"
)

Target "UpdateBinaries" (fun _ ->

    DeleteDir "release"
    Repository.clone "" "https://github.com/fsharp/FSharp.Charting.git" "release"
    Branches.checkoutBranch "release" "release"
    CopyFile "bin/FSharp.Charting.fsx" "release/FSharp.Charting.fsx"
    CopyFile "bin/FSharp.Charting.Gtk.fsx" "release/FSharp.Charting.Gtk.fsx"
    CopyRecursive "bin/v40" "release/bin" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "release" (sprintf """commit -a -m "Update binaries for version %s""" version) |> printfn "%s"
    Branches.push "release"
)

Target "Release" DoNothing

"UpdateDocs" ==> "Release"
"UpdateBinaries" ==> "Release"

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build target=<Target>' to verride

Target "All" DoNothing

"Clean"
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "UpdateFsxVersions"
  ==> "Build"
  ==> "RunTests"
  ==> "NuGet"
  ==> "All"


Run <| getBuildParamOrDefault "target" "All"
