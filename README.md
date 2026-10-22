# Foster for visionOS

This is a fork of [FosterFramework/Foster](https://github.com/FosterFramework/Foster), in its
author's words "a small cross-platform 2D-focused game framework in C#", based on
[v0.4.2](https://github.com/FosterFramework/Foster/releases/tag/v0.4.2). It adds what a Foster
game needs to render into the stereo drawables of Apple Vision Pro through
[CompositorServices](https://developer.apple.com/documentation/compositorservices): a
host-driven frame loop, render targets over textures the compositor owns, multiview, foveated
rendering with the compositor's rasterization rate map, and multisampling that resolves into
those textures. Everything is driven through SDL_GPU; Foster itself has no Metal code.

_Work in progress. The API of the added features may still change; the original Foster API is
unchanged._

## What the fork adds

Host-driven loop (`App.cs`):

- `App.Begin()`, `App.Tick(TimeSpan delta, bool present = true)` and `App.End()` let a host (for
  example a CompositorServices render loop) drive the frames with its own clock instead of
  `App.Run()`. `Tick` runs one update with the given delta, renders, and either presents to the
  windows or only submits the GPU work (`present: false`) when the host shows the image itself.
- `AppFlags.HiddenWindow` keeps the SDL window hidden for hosts that never show it.

Graphics device:

- `AppConfig.GraphicsDeviceProperties` passes SDL properties into the creation of the SDL_GPU
  device, so a host can hand over its own `MTLDevice` and `MTLCommandQueue` and register a
  command buffer completion callback (property names in
  `Framework/Internal/ThirdParty/SDL3.VisionOSKit.cs`).
- `GraphicsDevice.Capabilities` (`GraphicsCapabilities` with `KitExports`, `MultiView`,
  `PassProperties`, `ExternalTextures`, `Memoryless`, `DepthResolve`) reports which of the
  features below the loaded SDL library provides. They are probed by export name at start-up,
  never by version number; `GraphicsDevice.IsKitSDLLoaded()` answers the same question before a
  device exists. Features that are not available throw a `NotSupportedException` that names the
  missing capability.
- `GraphicsDevice.RenderPassesLastFrame` and `GraphicsDevice.DrawCallsLastFrame` count the render
  passes and draw calls between two presents or flushes.
- `AppFlags.DepthClip` creates the device without depth clamping and enables depth clip in every
  pipeline, the two settings SDL couples; on visionOS it is on automatically, because the Metal
  backend there rejects pipelines without depth clip.

Textures (`Texture.cs`):

- Array textures: `new Texture(graphicsDevice, width, height, format, layers, flags, properties,
  name)` creates a 2D array; `Texture.Layers`, `Texture.IsArray`, and layer-aware overloads of
  `SetData`, `GetData` and `Blit`. Array textures cannot be bound as 2D samplers; Foster rejects
  that before the pass begins.
- External textures: the same constructor with SDL properties that name an existing `MTLTexture`
  wraps it instead of allocating a new one (for example a compositor drawable). Needs
  `GraphicsCapabilities.ExternalTextures`.
- `TextureFlags.RenderTarget` marks a texture as a render target attachment for a `Target` over
  existing textures. `TextureFlags.Sampled` matters only for external textures: Foster's own
  textures stay readable by shaders and blits as before, while for an external texture Foster
  requests sampler usage only with this flag, because the wrapped `MTLTexture` must cover every
  usage that is requested and a compositor drawable may not be shader-readable.

Targets (`Target.cs`):

- `new Target(graphicsDevice, attachments, layer, name)` renders into one layer of existing
  textures without owning them (`Target.Layer`, `Target.OwnsAttachments`).
- `new Target(graphicsDevice, attachments, layer, sampleCount, multiSampleStore, depthResolve,
  colorMemoryless, depthMemoryless, viewCount, name)` adds multisampling over existing
  single-sample textures: Foster owns the multisampled copies and resolves them into the existing
  textures at the end of each pass, color through the pass's resolve texture, depth through the
  kit's depth resolve with a `DepthResolveFilter` (`Sample0`, `Min`, `Max`). `ColorMemoryless`
  and `DepthMemoryless` keep the samples in tile memory only; such a pass must start with a
  clear. Needs `GraphicsCapabilities.DepthResolve` and `Memoryless` respectively.
- `Target.MultiSampleStore` (default `true`) keeps the color samples after a pass so a later pass
  on the same Target can load them; `false` resolves only, which memoryless color requires.
- Multiview: `Target.ViewCount` above 1 renders every draw into that many consecutive layers in
  one pass (vertex amplification on Metal). Shaders without a view index render the same image
  into every view; shaders can read the view index to differ per view. Needs
  `GraphicsCapabilities.MultiView`. With more than one view the multisampled depth must be
  memoryless; Foster rejects the other combination with a message that says why.
- Rasterization rate map: `Target.SetRasterizationRateMap(nint map, int logicalWidth, int
  logicalHeight)` and `Target.ClearRasterizationRateMap()` attach an `MTLRasterizationRateMap`
  to the passes on this Target, per frame if the map changes. With a map, viewport, scissor and
  the `Batcher` projection work in the map's logical space: `Target.LogicalWidth`,
  `Target.LogicalHeight`, `Target.LogicalSize`, `Target.LogicalBounds`, and `WidthInPixels` and
  `HeightInPixels` of the `IDrawableTarget` interface report that size, while `Target.Width`,
  `Target.Height` and `Target.Bounds` stay the texture size. The Target has to render from
  layer 0 (view i uses map layer i). Needs `GraphicsCapabilities.PassProperties`.

Fixes that also apply without the kit:

- After a window size change (for example switching to fullscreen) the backbuffer follows the
  new size before the first draw of the frame, and a blit that does not cover the swapchain
  clears the rest instead of showing undefined memory.
- `Line.ClosestTUnclamped` returns 0 for a zero-length line instead of dividing 0 by 0.
- The `Target` constructor sets the device before its checks, so a failed check no longer
  crashes in the finalizer.
- `Texture.Blit` into part of a texture loads the existing content instead of discarding the
  rest of the texture.
- Multisampled color targets are stored as well as resolved (`Target.MultiSampleStore`), so a
  later pass on the same Target loads defined samples.
- A render pass that SDL refuses to begin throws an `InvalidOperationException` with SDL's
  message instead of dropping the draws silently.
- Input events are polled before the input state is updated, so they reach the game one frame
  earlier (upstream commit
  [da3b23a](https://github.com/FosterFramework/Foster/commit/da3b23a5f5ab4f1c21a360318095fefcbd826d79)).

## Requirements

- [dotnet 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), like Foster.
- For the visionOS features: the SDL_GPU kit for visionOS, a fork of SDL3 with the Metal-side
  additions these features use (shared device and queue, external textures, memoryless textures,
  multiview, rasterization rate maps, depth resolve):
  [LowRiderXR/sdl-visionos](https://github.com/LowRiderXR/sdl-visionos), branch `visionos-kit`.
- With a stock SDL 3.4.x the fork behaves like Foster v0.4.2 plus the fixes listed above:
  `GraphicsDevice.Capabilities` reports
  every feature as `false`, and using one of them throws a `NotSupportedException` that names the
  capability and the loaded SDL.

## Building and referencing

Clone the repository and add a reference to `Framework/Foster.Framework.csproj`, as with Foster.

By default the project copies Foster's prebuilt SDL3 library next to the output (on macOS also
for `osx-arm64`). To use the kit instead, set the MSBuild property `FosterUseExternalSDL=true`
when building the referencing project; Foster then copies no SDL library of its own, and the
referencing project places the kit's library next to the executable under the name Foster loads,
`libSDL3.dylib` on macOS (for example as a `Content` item with `Link="libSDL3.dylib"`). The
property has to be visible to this project's build, so pass it as a global property
(`dotnet build -p:FosterUseExternalSDL=true`) rather than only through the `ProjectReference`.

How to embed the kit in a visionOS app (as an embedded framework) is not described yet; it
follows once it has been tested on the device.

## Versioning

Versions are `0.4.2-visionos.N` (`Version` in `Framework/Foster.Framework.csproj`), tagged as
`v0.4.2-visionos.N`. The base stays Foster v0.4.2; the fork does not track Foster `main`.
Fixes that do not depend on the kit are candidates for upstream pull requests.

## License

MIT, like Foster (Copyright (c) 2023 Noel Berry, see `LICENSE`). The changes in this fork are
MIT as well.

## Note on AI assistance

Most of the code in this fork was written with the help of an AI coding assistant (Anthropic's
Claude) and tested by the maintainer on macOS (Apple silicon); testing on Apple Vision Pro is in
progress.

---

# Original Foster README

<p align="center">
<img width="480" src="Foster.png" alt="Foster logo">
</p>

# Foster
Foster is a small cross-platform 2D-focused game framework in C#.

_★ very work in progress! likely to have frequent, breaking changes! please use at your own risk! ★_

To use the framework either 
 - add a reference to the [NuGet package](https://www.nuget.org/packages/FosterFramework), 
 - or clone this repository and add a reference to `Foster/Framework/Foster.Framework.csproj`.

There is a [Samples](https://github.com/FosterFramework/Samples) repo which contains various demos and examples that can help you get started.

Check out [Discussons](https://github.com/FosterFramework/Foster/discussions) or [Discord](https://discord.gg/K7tdFuP3Bg) to get involved.

### Dependencies
 - [dotnet 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
 - [SDL3](https://github.com/libsdl-org/sdl), which has prebuilt binaries for various platforms through Github Actions.

### Rendering
 - Rendering is implemented using [SDL_GPU](https://wiki.libsdl.org/SDL3/CategoryGPU).
 - Shaders must follow the [SDL_GPU shader resource requirements](https://wiki.libsdl.org/SDL3/SDL_CreateGPUShader#remarks).
 - You can specify which Graphics Device to use when you run your Application.
 - You must provide shaders for the resulting Renderer (ex. SPIR-V for Vulkan, etc). There are built-in shaders for 2D rendering so this only matters if you write custom shaders.

### Notes
 - Taken a lot of inspiration from other Frameworks and APIs, namely [FNA](https://fna-xna.github.io/).
 - This is the second iteration of this library. The first [can be found here](https://github.com/NoelFB/fosterold).
 - Contributions are welcome! However, anything that adds external dependencies or complicates the build process will not be accepted.
