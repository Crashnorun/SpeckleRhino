# SpeckleRhino
This repository contains various projects for building Speckle Clients for Rhino and Grasshopper.
[SpeckleCore](https://github.com/speckleworks/SpeckleCore) is included as a submodule.

[![Build status](https://ci.appveyor.com/api/projects/status/mtfs3owdpy72yuh3/branch/master?svg=true)](https://ci.appveyor.com/project/SpeckleWorks/specklerhino/branch/master)

## Usage

If you want to install Speckle for Rhino, download the [latest build](https://ci.appveyor.com/api/projects/SpeckleWorks/SpeckleRhino/artifacts/specklerhino.rhi?branch=master&job=Configuration%3DRelease) from the `master` branch.

Alternatively, you can download the [latest offical release](https://github.com/speckleworks/SpeckleRhino/releases/latest).

## Building Speckle for Rhino

Speckle for Rhino is developed with Visual Studio 2017.

In order to build Speckle for Rhino:

1. Clone this repository
2. run `git submodule update --init` to bring in the SpeckleCore submodule
3. Open SpeckleRhino.sln
4. Build the solution

The complete build output will be found in the Solution Directory under the `Debug` or `Release` directory, depending on the configuration you are building.
