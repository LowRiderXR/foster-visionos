namespace Foster.Framework;

/// <summary>
/// A 2D Render Target used to draw content off-screen.
/// </summary>
public class Target : IGraphicResource, IDrawableTarget
{
	private static readonly (TextureFormat, TextureFlags, SampleCount)[] defaultFormats = [ (TextureFormat.Color, TextureFlags.None, SampleCount.One) ];

	/// <summary>
	/// The GraphicsDevice this Texture was created in
	/// </summary>
	public GraphicsDevice GraphicsDevice { get; private set; }

	/// <summary>
	/// Optional Target Name
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Ii the Target has been disposed.
	/// </summary>
	public bool IsDisposed => disposed || GraphicsDevice.Disposed;

	/// <summary>
	/// The Width of the Target.
	/// </summary>
	public readonly int Width;

	/// <summary>
	/// The Height of the Target.
	/// </summary>
	public readonly int Height;

	/// <summary>
	/// Target Bounds
	/// </summary>
	public readonly RectInt Bounds;

	/// <summary>
	/// The Texture attachments in the Target.
	/// </summary>
	public readonly Texture[] Attachments;

	/// <summary>
	/// The array layer of the attachments this Target renders into (0 for plain 2D textures).
	/// </summary>
	public readonly int Layer;

	/// <summary>
	/// True if the Target created its attachments and disposes them; false for a Target
	/// built over existing Textures, which stay owned by whoever created them.
	/// </summary>
	public readonly bool OwnsAttachments;

	/// <summary>
	/// Sample count of the render pass. For a Target over existing textures above one, Foster
	/// owns multisampled copies of the attachments and resolves into the existing textures.
	/// </summary>
	public readonly SampleCount SampleCount = SampleCount.One;

	/// <summary>
	/// True if the multisampled color attachments are memoryless (tile memory only, SDL_GPU
	/// kit): every pass on this Target must start with a clear, and the samples are only
	/// resolved, never stored (<see cref="MultiSampleStore"/> must be false).
	/// </summary>
	public readonly bool ColorMemoryless;

	/// <summary>
	/// True if the multisampled depth attachment is memoryless: it is resolved into the
	/// existing depth texture at the end of the pass and discarded; a later pass on this
	/// Target cannot load it, so every pass must clear depth.
	/// </summary>
	public readonly bool DepthMemoryless;

	/// <summary>
	/// Foster-owned multisampled copies of <see cref="Attachments"/> (Targets over existing
	/// textures with a sample count above one); the render pass draws into these and resolves
	/// into the attachments.
	/// </summary>
	internal readonly Texture[]? MultiSampleAttachments;

	/// <summary>
	/// The textures a render pass actually binds.
	/// </summary>
	internal Texture[] RenderAttachments => MultiSampleAttachments ?? Attachments;

	/// <summary>
	/// For multisampled color attachments: true (default) keeps the samples after a render
	/// pass in addition to resolving them (RESOLVE_AND_STORE), so a later pass on the same
	/// Target can load them; false only resolves (RESOLVE), which is enough for Targets that
	/// are drawn in a single pass and required for memoryless attachments.
	/// </summary>
	public readonly bool MultiSampleStore = true;

	/// <summary>
	/// For a multisampled depth attachment: the filter used to resolve it into the
	/// single-sample copy that shaders sample. Needs <see cref="GraphicsCapabilities.DepthResolve"/>;
	/// without it a multisampled depth texture cannot be sampled.
	/// </summary>
	public readonly DepthResolveFilter DepthResolve = DepthResolveFilter.Sample0;

	object? IDrawableTarget.Surface => this;
	int IDrawableTarget.WidthInPixels => Width;
	int IDrawableTarget.HeightInPixels => Height;

	internal readonly GraphicsDevice.ResourceHandle Resource;

	private bool disposed;

	/// <summary>
	/// Constructs a Target with a single <see cref="TextureFormat.Color"/> attachment. 
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, int width, int height, string? name = null)
		: this(graphicsDevice, width, height, defaultFormats, name) { }

	/// <summary>
	/// Constructs a Target with the given Attachments
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, int width, int height, in ReadOnlySpan<TextureFormat> attachments, TextureFlags flags = TextureFlags.None, string? name = null)
		: this(graphicsDevice, width, height, GetAttachments(attachments, flags), name) {}

	/// <summary>
	/// Constructs a Target with the given Attachments
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, int width, int height, in ReadOnlySpan<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> attachments, string? name = null, bool multiSampleStore = true, DepthResolveFilter depthResolve = DepthResolveFilter.Sample0)
		: this(graphicsDevice, width, height, GetAttachments(attachments), name, multiSampleStore, depthResolve) {}

	/// <summary>
	/// Constructs a Target over existing Textures without creating new ones. All attachments
	/// must come from the same device, have the same size and the same number of layers, and
	/// must have been created with <see cref="TextureFlags.RenderTarget"/>; the first color
	/// attachment is required, a depth attachment is optional. Rendering goes into
	/// <paramref name="layer"/> of every attachment. The Target does not own the Textures.
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, ReadOnlySpan<Texture> attachments, int layer = 0, string? name = null)
		: this(graphicsDevice, attachments, layer, SampleCount.One, true, DepthResolveFilter.Sample0, false, false, name) {}

	/// <summary>
	/// Constructs a multisampled Target over existing single-sample Textures: Foster creates
	/// the multisampled attachments itself (same format, size and layer count) and resolves
	/// them into the existing Textures at the end of each pass, color through the pass's
	/// resolve texture, depth through the SDL_GPU kit's depth resolve with
	/// <paramref name="depthResolve"/>, each into <paramref name="layer"/>. This is the way
	/// onto the compositor drawables of visionOS. Options: <paramref name="multiSampleStore"/>
	/// keeps the color samples for a later pass (default true, like every other Target: a
	/// forgotten setting costs bandwidth, never correctness; set false for single-pass targets);
	/// <paramref name="colorMemoryless"/> and <paramref name="depthMemoryless"/> keep the
	/// samples in tile memory only (needs <see cref="GraphicsCapabilities.Memoryless"/>; color
	/// requires <paramref name="multiSampleStore"/> set to false explicitly; every pass must
	/// then start with a clear of the memoryless attachments). Multisampled arrays and the depth resolve need the
	/// SDL_GPU kit; without it a <see cref="NotSupportedException"/> names what is missing.
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, ReadOnlySpan<Texture> attachments, int layer, SampleCount sampleCount, bool multiSampleStore = true, DepthResolveFilter depthResolve = DepthResolveFilter.Sample0, bool colorMemoryless = false, bool depthMemoryless = false, string? name = null)
	{
		// set first: the finalizer disposes through the device even if a check below throws
		GraphicsDevice = graphicsDevice;
		Name = name ?? string.Empty;

		if (attachments.Length <= 0)
			throw new ArgumentException("Target needs at least 1 color attachment");

		var first = attachments[0];
		var hasColor = false;
		var depthCount = 0;
		Span<GraphicsDevice.ResourceHandle> handles = stackalloc GraphicsDevice.ResourceHandle[attachments.Length];
		for (int i = 0; i < attachments.Length; i++)
		{
			var it = attachments[i];
			if (it.IsDisposed)
				throw new ArgumentException($"Attachment {i} is disposed");
			if (it.GraphicsDevice != graphicsDevice)
				throw new ArgumentException($"Attachment {i} belongs to another GraphicsDevice");
			if (it.Width != first.Width || it.Height != first.Height)
				throw new ArgumentException($"Attachment {i} is {it.Width}x{it.Height}, attachment 0 is {first.Width}x{first.Height}; all attachments must have the same size");
			if (it.Layers != first.Layers)
				throw new ArgumentException($"Attachment {i} has {it.Layers} layer(s), attachment 0 has {first.Layers}; all attachments must have the same layer count");
			if (it.SampleCount != SampleCount.One)
				throw new ArgumentException($"Attachment {i} is multisampled; Targets over existing textures take single-sample textures");
			if (!it.Flags.Has(TextureFlags.RenderTarget))
				throw new ArgumentException($"Attachment {i} was not created with TextureFlags.RenderTarget");
			if (!it.Format.IsDepthStencilFormat())
				hasColor = true;
			else
				depthCount++;
			handles[i] = it.Resource;
		}
		if (!hasColor)
			throw new ArgumentException("Target needs at least 1 color attachment");
		if (depthCount > 1)
			throw new ArgumentException($"Target supports at most one depth attachment, {depthCount} were given");
		if (layer < 0 || layer >= first.Layers)
			throw new ArgumentOutOfRangeException(nameof(layer), $"Layer {layer} is out of range, the attachments have {first.Layers} layer(s)");

		// multisampled over existing textures: what the combination needs, with a clear message
		var caps = graphicsDevice.Capabilities;
		if (sampleCount == SampleCount.One)
		{
			if (colorMemoryless || depthMemoryless)
				throw new ArgumentException("Memoryless attachments need a sample count above one; single-sample attachments are the existing textures themselves");
		}
		else
		{
			if (first.Layers > 1 && !caps.KitExports)
				throw new NotSupportedException($"Multisampled array textures need the SDL_GPU kit for visionOS; the loaded SDL reports {caps}");
			if (depthCount > 0 && !caps.DepthResolve)
				throw new NotSupportedException($"Resolving a multisampled depth attachment into an existing depth texture needs the depth resolve of the SDL_GPU kit (GraphicsCapabilities.DepthResolve); the loaded SDL reports {caps}");
			if ((colorMemoryless || depthMemoryless) && !caps.Memoryless)
				throw new NotSupportedException($"Memoryless attachments need the SDL_GPU kit for visionOS with the Metal driver (GraphicsCapabilities.Memoryless); the loaded SDL reports {caps}");
			if (colorMemoryless && multiSampleStore)
				throw new ArgumentException("Memoryless color attachments cannot keep their samples: set multiSampleStore to false (RESOLVE only) or make the color not memoryless");
		}

		Width = first.Width;
		Height = first.Height;
		Bounds = new RectInt(0, 0, Width, Height);
		Attachments = attachments.ToArray();
		Layer = layer;
		OwnsAttachments = false;
		SampleCount = sampleCount;
		MultiSampleStore = sampleCount == SampleCount.One || multiSampleStore;
		DepthResolve = depthResolve;
		ColorMemoryless = sampleCount != SampleCount.One && colorMemoryless;
		DepthMemoryless = sampleCount != SampleCount.One && depthMemoryless;
		Resource = GraphicsDevice.CreateTarget(handles, layer, MultiSampleStore, depthResolve);

		if (sampleCount != SampleCount.One)
		{
			// Foster-owned multisampled copies, each resolving into its existing texture
			MultiSampleAttachments = new Texture[Attachments.Length];
			for (int i = 0; i < Attachments.Length; i++)
			{
				var it = Attachments[i];
				var isDepth = it.Format.IsDepthStencilFormat();
				var attachmentName = !string.IsNullOrEmpty(name) ? $"{Name}-MSAA{i}" : null;
				MultiSampleAttachments[i] = new Texture(graphicsDevice, Width, Height, it.Format, TextureFlags.None, sampleCount, this, attachmentName, it.Layers, 0,
					resolveInto: it.Resource, memoryless: isDepth ? DepthMemoryless : ColorMemoryless);
			}
		}
	}

	private Target(GraphicsDevice graphicsDevice, int width, int height, in StackList16<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> attachments, string? name = null, bool multiSampleStore = true, DepthResolveFilter depthResolve = DepthResolveFilter.Sample0)
	{
		// set first: the finalizer disposes through the device even if a check below throws
		GraphicsDevice = graphicsDevice;
		Name = name ?? string.Empty;

		if (width <= 0 || height <= 0)
			throw new ArgumentException("Target width and height must be larger than 0");

		if (attachments.Count <= 0)
			throw new ArgumentException("Target needs at least 1 color attachment");

		MultiSampleStore = multiSampleStore;
		DepthResolve = depthResolve;
		Resource = GraphicsDevice.CreateTarget(width, height, multiSampleStore, depthResolve);
		Width = width;
		Height = height;
		Bounds = new RectInt(0, 0, Width, Height);
		Layer = 0;
		OwnsAttachments = true;
		Attachments = new Texture[attachments.Count];
		for (int i = 0; i < attachments.Count; i ++)
		{
			var attachmentName = !string.IsNullOrEmpty(name) ? $"{Name}-Attachment{i}" : null;
			Attachments[i] = new Texture(graphicsDevice, width, height, attachments[i].Format, attachments[i].Flags, attachments[i].SampleCount, this, attachmentName);
		}
	}

	~Target() => Dispose();

	private static StackList16<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> GetAttachments(in ReadOnlySpan<TextureFormat> values, TextureFlags flags)
	{
		StackList16<(TextureFormat, TextureFlags, SampleCount)> result = [];
		for (int i = 0; i < values.Length; i ++)
			result.Add((values[i], flags, SampleCount.One));
		return result;
	}

	private static StackList16<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> GetAttachments(in ReadOnlySpan<(TextureFormat, TextureFlags, SampleCount)> values)
		=> [..values];

	/// <summary>
	/// Disposes of the Target and all its Attachments
	/// </summary>
	public void Dispose()
	{
		if (!disposed)
		{
			GraphicsDevice.DestroyResource(Resource);
			disposed = true;
		}

		GC.SuppressFinalize(this);
	}

	public static implicit operator Texture(Target target) => target.Attachments[0];
}
