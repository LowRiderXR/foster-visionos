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
	public Target(GraphicsDevice graphicsDevice, int width, int height, in ReadOnlySpan<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> attachments, string? name = null)
		: this(graphicsDevice, width, height, GetAttachments(attachments), name) {}

	/// <summary>
	/// Constructs a Target over existing Textures without creating new ones. All attachments
	/// must come from the same device, have the same size and the same number of layers, and
	/// must have been created with <see cref="TextureFlags.RenderTarget"/>; the first color
	/// attachment is required, a depth attachment is optional. Rendering goes into
	/// <paramref name="layer"/> of every attachment. The Target does not own the Textures.
	/// </summary>
	public Target(GraphicsDevice graphicsDevice, ReadOnlySpan<Texture> attachments, int layer = 0, string? name = null)
	{
		// set first: the finalizer disposes through the device even if a check below throws
		GraphicsDevice = graphicsDevice;
		Name = name ?? string.Empty;

		if (attachments.Length <= 0)
			throw new ArgumentException("Target needs at least 1 color attachment");

		var first = attachments[0];
		var hasColor = false;
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
			handles[i] = it.Resource;
		}
		if (!hasColor)
			throw new ArgumentException("Target needs at least 1 color attachment");
		if (layer < 0 || layer >= first.Layers)
			throw new ArgumentOutOfRangeException(nameof(layer), $"Layer {layer} is out of range, the attachments have {first.Layers} layer(s)");

		Width = first.Width;
		Height = first.Height;
		Bounds = new RectInt(0, 0, Width, Height);
		Attachments = attachments.ToArray();
		Layer = layer;
		OwnsAttachments = false;
		Resource = GraphicsDevice.CreateTarget(handles, layer);
	}

	private Target(GraphicsDevice graphicsDevice, int width, int height, in StackList16<(TextureFormat Format, TextureFlags Flags, SampleCount SampleCount)> attachments, string? name = null)
	{
		// set first: the finalizer disposes through the device even if a check below throws
		GraphicsDevice = graphicsDevice;
		Name = name ?? string.Empty;

		if (width <= 0 || height <= 0)
			throw new ArgumentException("Target width and height must be larger than 0");

		if (attachments.Count <= 0)
			throw new ArgumentException("Target needs at least 1 color attachment");

		Resource = GraphicsDevice.CreateTarget(width, height);
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
