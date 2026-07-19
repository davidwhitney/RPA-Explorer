# RPA Explorer

Graphical explorer for RenPy Archives. This tool brings ability to extract, create new or change existing RPA archives all in one window. It also provides content preview for most common files in these packages. Initial parser code was inspired by [RPATools](https://github.com/Shizmob/rpatool), so in case you find this tool useful, go give them a thumbs up as well. Now it even can try to preview compiled RenPy files (conditions apply, see <sup>[[1]](#reference1)</sup>).

#### Note:

This is a fan made application and there is no guarantee of further development or fixes. For video support LibVLC library is used and this library has ~300MiB in size so this is the reason why this application is so big, I haven't found a better way around this yet.

#### Cross-platform (modern .NET + Avalonia):

The application has been ported from Windows Forms (.NET Framework 4.6.1) to [Avalonia UI](https://avaloniaui.net/) on **.NET 8**, so it now runs on **macOS (incl. Apple Silicon), Linux and Windows**.

What changed under the hood:
- UI rebuilt in Avalonia (was Windows Forms).
- `Ionic.Zlib` replaced with the built-in `System.IO.Compression.ZLibStream`.
- Image/WebP decoding uses [ImageSharp](https://github.com/SixLabors/ImageSharp) instead of `System.Drawing` + the native WebP wrapper.
- Video/audio preview uses `LibVLCSharp.Avalonia`. On **macOS and Linux** it binds to a system-wide VLC installation; on **Windows** the native libraries come from the `VideoLAN.LibVLC.Windows` package. (The `VideoLAN.LibVLC.Mac` NuGet package is deliberately not used: it ships only an x86_64 `libvlc.dylib` with no plugin set, so it cannot work on Apple Silicon.)
- Python 2.7 detection is now cross-platform (searches `PATH` and common install locations); you can still override it via **Options**.
- Windows-only features that don't apply elsewhere (registry file-association) are hidden on non-Windows platforms.

##### Requirements

- [.NET SDK 8.0 or newer](https://dotnet.microsoft.com/download)
- **For audio/video preview on macOS and Linux:** [VLC](https://www.videolan.org/vlc/) must be installed
  (on macOS, `VLC.app` in `/Applications`; or `brew install --cask vlc`). The app locates it automatically
  and uses its libraries and codec plugins. Everything else (browsing, extracting, creating archives,
  image and text preview) works without VLC.

##### Build & run

```bash
# from the repository root
dotnet build "RPA Explorer.sln"
dotnet run --project "RPA Explorer/RPA Explorer.csproj"

# optionally open an archive directly
dotnet run --project "RPA Explorer/RPA Explorer.csproj" -- /path/to/archive.rpa
```

##### Producing release binaries

`build.sh` cross-compiles every supported platform from a single machine into `./dist`
(git-ignored). Builds are self-contained, so users do not need the .NET runtime installed.

```bash
./build.sh                                  # all platforms, version from the current git tag
./build.sh --version 1.2.3                  # explicit version
./build.sh --rids "osx-arm64 win-x64"       # subset of platforms
./build.sh --framework-dependent            # smaller, requires .NET on the target
```

Targets: `osx-arm64`, `osx-x64`, `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`.
macOS artifacts are packaged as a `RPA Explorer.app` bundle, Windows as a zip and Linux as a
tarball, alongside a `SHA256SUMS` file.

Pushing a `v*` tag runs the same script in GitHub Actions and publishes the artifacts to a
GitHub release (see `.github/workflows/build.yml`).

> The macOS builds are not code-signed or notarised. On first launch use right-click → Open,
> or run `xattr -dr com.apple.quarantine "RPA Explorer.app"`.

### Supported file types for preview:

- Text: py, rpy~, rpy, txt, log, nfo, htm, html, xml, json, yaml, csv
- Video: 3gp, flv, mov, mp4, ogv, swf, mpg, mpeg, avi, mkv, wmv, webm
- Audio: aac, ac3, flac, mp3, wma, wav, ogg, cpc
- Images: jpeg, jpg, bmp, tiff, png, webp, exif, ico, gif
- Compilations<sup>[[1]](#reference1)</sup>: rpyc~, rpymc~, rpyc, rpymc

### References

<a name="reference1"></a>[1]: Path to Python 2.7 environment and [unrpyc](https://github.com/CensoredUsername/unrpyc) on your local machine must be provided to attempt decompilation.

---

### Download link:

Pre-built binaries for macOS, Windows and Linux are attached to each
[GitHub release](https://github.com/UniverseDevel/RPA-Explorer/releases).

---

### TODO List:

[TODO.md](https://github.com/UniverseDevel/RPA-Explorer/blob/master/TODO.md)

### Known Issues:

The three issues listed here previously were addressed by the Avalonia port:

- ~~Selecting/unselecting objects too fast will not update selections for child or parent
  objects.~~ The tree is now backed by a data model whose check state is propagated in code
  rather than by WinForms `TreeView` events, so the result no longer depends on event timing.
- ~~Some video/audio formats will not update time played or total video time.~~ The total
  time is now taken from `MediaPlayer.Length` and refreshed on `LengthChanged`, instead of
  `Media.Duration` which stays at `-1` for media fed through a stream. Where a format genuinely
  never reports a length, the elapsed time is shown against `--:--:--` rather than a stale total.
- ~~When browsing through videos the application freezes after a while.~~ Each preview disposes
  its `Media`, `StreamMediaInput` and `MemoryStream`, a single `MediaPlayer` is reused, and
  libvlc is released on exit.

#### Images preview:
![1](https://user-images.githubusercontent.com/47400898/154856556-1da3d011-5631-4100-972c-f6e844967242.png)
#### Video preview:
![2](https://user-images.githubusercontent.com/47400898/154856560-71837ed7-899c-43bb-ab0d-3a10dd7844e8.png)
#### Text files preview:
![3](https://user-images.githubusercontent.com/47400898/154856564-1a588bdd-3412-491d-a070-078e17c42d19.png)

The software is provided "as is", without a warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and non-infringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
