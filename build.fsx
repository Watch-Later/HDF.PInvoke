#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.PaketTemplate
open Fake.ReleaseNotesHelper
open Fake.AssemblyInfoFile
open System

let allReleaseNotes =
    System.IO.File.ReadAllLines( "RELEASE_NOTES.md" )
    |> parseAllReleaseNotes

let VER =
    if hasBuildParam "ver" then
        getBuildParam "ver"
    else
        let ver =
            let sv = (Seq.head allReleaseNotes).SemVer
            sprintf "%i.%i" sv.Major sv.Minor
        tracefn "Auto choosed version: %s" ver
        ver

let name = sprintf "HDF5 %s" VER

let slnConfiguration = sprintf "HDF5 %s Release" VER

let fileAssemblyInfo = "Properties/AssemblyInfo.cs"

let releaseNotes =
    allReleaseNotes
    |> Seq.find (fun r -> r.NugetVersion |> startsWith VER)

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "obj"; "temp"]
)

Target "UpdateAssemblyInfo" (fun _ ->
    UpdateAttributes
        "Properties/AssemblyInfo.cs"
        [Attribute.Version releaseNotes.AssemblyVersion]
)

Target "Build" (fun _ ->
    "HDF.PInvoke.sln"
    |> build (fun p ->
        {p with
            Verbosity = Some Minimal
            NoLogo = true
            Targets = ["Build"]
            Properties =
                ["Configuration", slnConfiguration
                 "WarningLevel", "0"]
        })
)

Target "RunTests" (fun _ ->
    !! ("UnitTests/bin" </> slnConfiguration </> "UnitTests.dll" )
    |> MSTest.MSTest (fun p ->
        {p with
            TimeOut = TimeSpan.FromMinutes 5.
            ResultsDir = "TestResults"
            NoIsolation = true
        })
)

Target "GenTemplate" (fun _ ->
    PaketTemplate (fun p ->
        {p with
            TemplateFilePath = Some "paket.template"
            TemplateType = File

            Id = Some "HDF.PInvoke"
            Title = Some "HDF.PInvoke"
            LicenseUrl = Some "http://www.hdfgroup.org/ftp/HDF5/current/src/unpacked/COPYING"
            IconUrl = Some "https://raw.githubusercontent.com/HDFGroup/HDF.PInvoke/master/images/hdf.png"
            ProjectUrl = Some "https://github.com/HDFGroup/HDF.PInvoke"
            Copyright = Some "Copyright 2016"
            Language = Some "en-US"
            Description =
                [
                """This package contains PInvoke declarations for the (unmanaged) HDF5 """ + VER + """.x C-API. For documenation, see the "HDF5: API Specification Reference Manual" at http://www.hdfgroup.org/HDF5/doc/RM/RM_H5Front.html."""
                ]
            Owners = ["The HDF Group"]
            Authors =
                [
                "The HDF Group"
                //"DSanchen"
                //"gheber"
                //"hokb"
                ]
            Files =
                [
                // https://docs.nuget.org/ndocs/create-packages/creating-a-package#from-a-convention-based-working-directory
                Include ("build/**/*.*", "build")
                Include ("../../bin" </> slnConfiguration </> "*.*", "lib")
                ]
        })
)

Target "NuGet" (fun _ ->
    Paket.Pack (fun p ->
        {p with
            TemplateFile = "paket.template"
            
            Version = releaseNotes.NugetVersion
            ReleaseNotes = toLines releaseNotes.Notes
        })
)

Target "Deploy" (fun _ ->
    Paket.Push (fun p ->
        {p with
            EndPoint = "https://www.nuget.org/"
            ApiKey = ""
        })
)

Target "Rebuild" DoNothing

"Rebuild" <== [
    "Clean"
    "Build"
]

"Clean" =?> ("Build", hasBuildParam "Clean")
    
"UpdateAssemblyInfo"
 ==> "Build"
 ==> "RunTests"
 ==> "GenTemplate"
 ==> "NuGet"
 ==> "Deploy"

RunTargetOrDefault "RunTests"

