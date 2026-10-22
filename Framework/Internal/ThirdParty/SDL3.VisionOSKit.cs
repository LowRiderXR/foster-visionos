// SDL3.VisionOSKit.cs - bindings for the SDL_GPU kit for visionOS (fork sdl-visionos, branch
// visionos-kit). Kept out of the generated SDL3.cs so the generator diff stays readable.
//
// Only two functions are new compared to SDL 3.4.12; everything else is property names, which
// an unpatched SDL ignores. Whether the kit is present is decided at start-up by probing the
// exports (GraphicsDeviceSDL.ProbeCapabilities), never by version number. Nothing in here may
// be called unless GraphicsDevice.Capabilities says so.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL3;

public static unsafe partial class SDL
{
	// Names of the exports the kit adds; probed with NativeLibrary.TryGetExport.
	public const string KitExport_BeginGPURenderPassWithProperties = "SDL_BeginGPURenderPassWithProperties";
	public const string KitExport_GPUSupportsViewCount = "SDL_GPUSupportsViewCount";

	// Kit functions: a render pass with properties (view count, rasterization rate map, depth
	// resolve) and the query whether the device renders a given number of views in one pass.

	[LibraryImport(nativeLibName)]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	public static partial IntPtr SDL_BeginGPURenderPassWithProperties(IntPtr command_buffer, Span<SDL_GPUColorTargetInfo> color_target_infos, uint num_color_targets, in SDL_GPUDepthStencilTargetInfo depth_stencil_target_info, uint props);

	[LibraryImport(nativeLibName)]
	[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
	public static partial SDLBool SDL_GPUSupportsViewCount(IntPtr device, uint view_count);

	// Kit device properties: create the SDL_GPU device on a host's MTLDevice and MTLCommandQueue,
	// register a completion callback for every command buffer, read the device and queue back.
	public const string SDL_PROP_GPU_DEVICE_CREATE_METAL_DEVICE_POINTER = "SDL.gpu.device.create.metal.device";
	public const string SDL_PROP_GPU_DEVICE_CREATE_METAL_COMMAND_QUEUE_POINTER = "SDL.gpu.device.create.metal.command_queue";
	public const string SDL_PROP_GPU_DEVICE_CREATE_METAL_COMMAND_BUFFER_COMPLETED_CALLBACK_POINTER = "SDL.gpu.device.create.metal.command_buffer_completed_callback";
	public const string SDL_PROP_GPU_DEVICE_CREATE_METAL_COMMAND_BUFFER_COMPLETED_USERDATA_POINTER = "SDL.gpu.device.create.metal.command_buffer_completed_userdata";
	public const string SDL_PROP_GPU_DEVICE_METAL_DEVICE_POINTER = "SDL.gpu.device.metal.device";
	public const string SDL_PROP_GPU_DEVICE_METAL_COMMAND_QUEUE_POINTER = "SDL.gpu.device.metal.command_queue";

	// Kit texture properties: wrap an existing MTLTexture, create a memoryless (tile memory only) texture.
	public const string SDL_PROP_GPU_TEXTURE_CREATE_METAL_TEXTURE_POINTER = "SDL.gpu.texture.create.metal.texture";
	public const string SDL_PROP_GPU_TEXTURE_CREATE_METAL_MEMORYLESS_BOOLEAN = "SDL.gpu.texture.create.metal.memoryless";

	// Kit pipeline and render pass properties: view count (vertex amplification), rasterization
	// rate map, depth resolve texture, layer and filter.
	public const string SDL_PROP_GPU_GRAPHICSPIPELINE_CREATE_VIEW_COUNT_NUMBER = "SDL.gpu.graphicspipeline.create.view_count";
	public const string SDL_PROP_GPU_RENDERPASS_CREATE_VIEW_COUNT_NUMBER = "SDL.gpu.renderpass.create.view_count";
	public const string SDL_PROP_GPU_RENDERPASS_CREATE_METAL_RASTERIZATION_RATE_MAP_POINTER = "SDL.gpu.renderpass.create.metal.rasterization_rate_map";
	public const string SDL_PROP_GPU_RENDERPASS_CREATE_METAL_DEPTH_RESOLVE_TEXTURE_POINTER = "SDL.gpu.renderpass.create.metal.depth_resolve_texture";
	public const string SDL_PROP_GPU_RENDERPASS_CREATE_METAL_DEPTH_RESOLVE_LAYER_NUMBER = "SDL.gpu.renderpass.create.metal.depth_resolve_layer";
	public const string SDL_PROP_GPU_RENDERPASS_CREATE_METAL_DEPTH_RESOLVE_FILTER_STRING = "SDL.gpu.renderpass.create.metal.depth_resolve_filter";

	/// <summary>
	/// Kit: callback type for <see cref="SDL_PROP_GPU_DEVICE_CREATE_METAL_COMMAND_BUFFER_COMPLETED_CALLBACK_POINTER"/>:
	/// void (*)(void *userdata, void *mtl_command_buffer). Store an [UnmanagedCallersOnly] function
	/// pointer as the property value (AOT compatible, no delegate marshalling).
	/// </summary>
	public delegate void SDL_GPUMetalCommandBufferCompletedCallbackSignature(IntPtr userdata, IntPtr mtl_command_buffer);
}
