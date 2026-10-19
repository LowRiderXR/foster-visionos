namespace Foster.Framework;

/// <summary>
/// Optional features of the underlying SDL_GPU library, probed once when the device is created.
/// All of them are false with a stock SDL; they are true with the SDL_GPU kit for visionOS
/// (fork sdl-visionos, branch visionos-kit), whose exports are checked by name. Code paths that
/// need a feature must check the flag and fall back or throw a descriptive exception.
/// </summary>
/// <param name="KitExports">The kit's exports (SDL_BeginGPURenderPassWithProperties, SDL_GPUSupportsViewCount) are present in the loaded SDL library.</param>
/// <param name="MultiView">Render passes and pipelines with a view count above 1 (vertex amplification on Metal).</param>
/// <param name="PassProperties">SDL_BeginGPURenderPassWithProperties is available (rasterization rate map, depth resolve, view count).</param>
/// <param name="ExternalTextures">Textures can wrap an existing MTLTexture (kit and Metal driver).</param>
/// <param name="Memoryless">Textures can be created memoryless (kit and Metal driver).</param>
/// <param name="DepthResolve">Depth can be resolved through render pass properties (kit and Metal driver).</param>
public readonly record struct GraphicsCapabilities(
	bool KitExports,
	bool MultiView,
	bool PassProperties,
	bool ExternalTextures,
	bool Memoryless,
	bool DepthResolve)
{
	public static readonly GraphicsCapabilities None = new(false, false, false, false, false, false);

	public override string ToString()
		=> $"kitExports={KitExports} multiView={MultiView} passProperties={PassProperties} externalTextures={ExternalTextures} memoryless={Memoryless} depthResolve={DepthResolve}";
}
