# NcmSharpCLI

A CLI ncm conversion tool

Can convert encrypted audio files in ncm format to flac or mp3 audio files

This software is intended for learning and research. Please comply with **local laws and regulations**, and abide the [project license](https://github.com/LeZi9916/NcmSharpCLI/blob/master/LICENSE.txt).

## Framework

- .NET 9.0
- C# 13
- Support NativeAoT

## Usage

```bash
    NcmSharpCLI [options]

Options:
    -p,--working-path <path>    Set the directory for storing ncm encrypted audio files
    -o,--output-path  <path>    Set the output directory
    -j,--jobs         <count>   Set the maximum decryption workflow
                                WARNNING: This option is used to specify the maximum asynchronous workflow limit,
                                which does not necessarily use all threads.
    -c,--use-memory-as-cache    Read the file and store it in the memory buffer before decrypting it.
    -i,--igonre-extension       Ignore file extensions and attempt to decrypt all files
    -h,--help                   print help and exit
```

## Build

> Envirnemt
>
> Ubuntu 24.04.1 LTS

Before building, please make sure you have .NET 9.0 SDK on your computer

See also: [Install .NET on Windows, Linux, and macOS](https://learn.microsoft.com/en-us/dotnet/core/install/)

```bash
git clone https://github.com/LeZi9916/NcmSharpCLI

cd NcmSharpCLI
git submodule update --init --recursive

dotnet restore
dotnet build -c Release
# Build output path: bin/Release/net9.0/
```

## License

MIT license

## References

- [Majjcom/ncmpp](https://github.com/Majjcom/ncmpp)