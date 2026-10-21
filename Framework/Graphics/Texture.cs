using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Foster.Framework;

/// <summary>
/// A 2D Texture used for Rendering
/// </summary>
public class Texture : IGraphicResource
{
	/// <summary>
	/// The GraphicsDevice this Texture was created in
	/// </summary>
	public readonly GraphicsDevice GraphicsDevice;

	/// <summary>
	/// Optional Texture Name
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// If the Texture has been disposed
	/// </summary>
	public bool IsDisposed => disposed || GraphicsDevice.Disposed;

	/// <summary>
	/// Gets the Width of the Texture
	/// </summary>
	public readonly int Width;

	/// <summary>
	/// Gets the Height of the Texture
	/// </summary>
	public readonly int Height;

	/// <summary>
	/// Gets the Size (Width, Height) of the Texture
	/// </summary>
	public Point2 Size => new(Width, Height);

	/// <summary>
	/// The Texture Data Format
	/// </summary>
	public readonly TextureFormat Format;

	/// <summary>
	/// The Texture Sample Count. This is always <see cref="SampleCount.One"/> unless created as a <see cref="Target"/> attachment.
	/// </summary>
	public readonly SampleCount SampleCount;

	/// <summary>
	/// If this Texture is an Attachment for a Render Target.
	/// </summary>
	public readonly bool IsTargetAttachment;

	/// <summary>
	/// Number of array layers. 1 for a plain 2D texture; above 1 the texture is a 2D array
	/// (<see cref="Texture(GraphicsDevice, int, int, TextureFormat, int, TextureFlags, uint, string?)"/>).
	/// Array textures can only be rendered into through a <see cref="Target"/> bound to one
	/// layer; upload, download and blit need the overloads with a layer, and they cannot be
	/// sampled as 2D textures.
	/// </summary>
	public readonly int Layers;

	/// <summary>
	/// The flags the Texture was created with.
	/// </summary>
	public readonly TextureFlags Flags;

	/// <summary>
	/// The Memory Size of the Texture, in bytes
	/// </summary>
	public int MemorySize => Width * Height * Format.Size() * Layers;

	/// <summary>
	/// True if this is a 2D array (<see cref="Layers"/> above 1).
	/// </summary>
	public bool IsArray => Layers > 1;

	internal readonly GraphicsDevice.ResourceHandle Resource;

	private bool disposed;

	public Texture(GraphicsDevice graphicsDevice, int width, int height, TextureFormat format = TextureFormat.Color, TextureFlags flags = TextureFlags.None, string? name = null)
		: this(graphicsDevice, width, height, format, flags, SampleCount.One, targetBinding: null, name) {}

	public Texture(GraphicsDevice graphicsDevice, int width, int height, ReadOnlySpan<Color> pixels, TextureFlags flags = TextureFlags.None, string? name = null)
		: this(graphicsDevice, width, height, TextureFormat.Color, flags, name) => SetData<Color>(pixels);

	public Texture(GraphicsDevice graphicsDevice, int width, int height, ReadOnlySpan<byte> pixels, TextureFlags flags = TextureFlags.None, string? name = null)
		: this(graphicsDevice, width, height, TextureFormat.Color, flags, name) => SetData<byte>(pixels);

	public Texture(GraphicsDevice graphicsDevice, Image image, TextureFlags flags, string? name = null)
		: this(graphicsDevice, image.Width, image.Height, TextureFormat.Color, flags, name) => SetData<Color>(image.Data);

	public Texture(GraphicsDevice graphicsDevice, Image image, string? name = null)
		: this(graphicsDevice, image.Width, image.Height, TextureFormat.Color, TextureFlags.None, name) => SetData<Color>(image.Data);

	/// <summary>
	/// Creates a Texture with <paramref name="layers"/> array layers (above 1 gives a 2D array)
	/// and optional SDL properties that are passed to SDL_CreateGPUTexture. A host uses the
	/// properties to wrap an existing native texture (for example a compositor drawable); such
	/// external textures need <see cref="GraphicsCapabilities.ExternalTextures"/>, otherwise a
	/// <see cref="NotSupportedException"/> is thrown. Foster releases only its own handle; the
	/// native texture stays owned by the host. The properties are not destroyed by Foster.
	/// Use <see cref="TextureFlags.RenderTarget"/> to attach the texture to a Target created
	/// from existing textures.
	/// </summary>
	public Texture(GraphicsDevice graphicsDevice, int width, int height, TextureFormat format, int layers, TextureFlags flags = TextureFlags.None, uint properties = 0, string? name = null)
		: this(graphicsDevice, width, height, format, flags, SampleCount.One, targetBinding: null, name, layers, properties) {}

	internal Texture(GraphicsDevice graphicsDevice, int width, int height, TextureFormat format, TextureFlags flags, SampleCount sampleCount, Target? targetBinding, string? name, int layers = 1, uint properties = 0)
	{
		GraphicsDevice = graphicsDevice;

		if (width <= 0 || height <= 0)
			throw new Exception("Texture must have a size larger than 0");

		if (layers <= 0)
			throw new ArgumentOutOfRangeException(nameof(layers), "Texture must have at least 1 layer");

		Resource = graphicsDevice.CreateTexture(name, width, height, format, flags, sampleCount, targetBinding?.Resource, layers, properties);
		Name = name ?? string.Empty;
		Width = width;
		Height = height;
		Format = format;
		Flags = flags;
		SampleCount = sampleCount;
		Layers = layers;
		IsTargetAttachment = targetBinding != null;
	}

	private void RequireNotArray(string operation)
	{
		if (IsArray)
			throw new InvalidOperationException($"{operation} is not supported on an array texture with {Layers} layers; use the overload that names a layer");
	}

	private void RequireLayer(int layer)
	{
		if (layer < 0 || layer >= Layers)
			throw new ArgumentOutOfRangeException(nameof(layer), $"Layer {layer} is out of range, the texture has {Layers} layer(s)");
	}

	~Texture() => Dispose(false);

	/// <summary>
	/// Sets the Texture data from the given buffer
	/// </summary>
	public void SetData<T>(ReadOnlySpan<T> data) where T : struct
		=> SetData(data, new RectInt(0, 0, Width, Height));

	/// <summary>
	/// Sets the Texture data in a region from the given buffer
	/// </summary>
	public void SetData<T>(ReadOnlySpan<T> data, RectInt destRegion) where T : struct
	{
		RequireNotArray("SetData without a layer");
		SetData(data, destRegion, 0);
	}

	/// <summary>
	/// Sets the Texture data in a region of one array layer from the given buffer
	/// </summary>
	public unsafe void SetData<T>(ReadOnlySpan<T> data, RectInt destRegion, int layer) where T : struct
	{
		if (IsDisposed)
			throw new Exception("Resource is Disposed");

		RequireLayer(layer);

		if (destRegion.Left < 0 || destRegion.Top < 0 || destRegion.Bottom > Height || destRegion.Right > Width)
			throw new Exception("Destination region is out of range");

		int dataLength = Unsafe.SizeOf<T>() * data.Length;

		if (dataLength < destRegion.Width * destRegion.Height * Format.Size())
			throw new Exception("Data Buffer is smaller than the Size of the Texture Destination");

		fixed (byte* ptr = MemoryMarshal.AsBytes(data))
		{
			GraphicsDevice.SetTextureData(Resource, new nint(ptr), dataLength, destRegion, layer);
		}
	}

	/// <summary>
	/// Writes the Texture data to the given buffer
	/// </summary>
	public void GetData<T>(Span<T> data) where T : struct
		=> GetData(data, new RectInt(0, 0, Width, Height));

	/// <summary>
	/// Writes the Texture data from a region to the given buffer
	/// </summary>
	public void GetData<T>(Span<T> data, RectInt sourceRegion) where T : struct
	{
		RequireNotArray("GetData without a layer");
		GetData(data, sourceRegion, 0);
	}

	/// <summary>
	/// Writes the Texture data of a region of one array layer to the given buffer
	/// </summary>
	public unsafe void GetData<T>(Span<T> data, RectInt sourceRegion, int layer) where T : struct
	{
		if (IsDisposed)
			throw new Exception("Resource is Disposed");

		RequireLayer(layer);

		if (sourceRegion.Left < 0 || sourceRegion.Top < 0 || sourceRegion.Bottom > Height || sourceRegion.Right > Width)
			throw new Exception("Source region is out of range");

		int dataLength = Unsafe.SizeOf<T>() * data.Length;

		if (dataLength < sourceRegion.Width * sourceRegion.Height * Format.Size())
			throw new Exception("Data Buffer is smaller than the Size of the Texture Source");

		fixed (byte* ptr = MemoryMarshal.AsBytes(data))
		{
			GraphicsDevice.GetTextureData(Resource, new nint(ptr), dataLength, sourceRegion, layer);
		}
	}

	/// <summary>
	/// Blits the contents of this Texture to another Texture
	/// </summary>
	public void Blit(RectInt sourceRect, Texture destination, RectInt destinationRect, TextureFilter filter)
	{
		RequireNotArray("Blit without a layer");
		destination.RequireNotArray("Blit into an array texture without a layer");
		GraphicsDevice.BlitTexture(Resource, sourceRect, destination.Resource, destinationRect, filter);
	}

	/// <summary>
	/// Blits one array layer of this Texture to one array layer of another Texture
	/// </summary>
	public void Blit(RectInt sourceRect, int sourceLayer, Texture destination, RectInt destinationRect, int destinationLayer, TextureFilter filter)
	{
		RequireLayer(sourceLayer);
		destination.RequireLayer(destinationLayer);
		GraphicsDevice.BlitTexture(Resource, sourceRect, destination.Resource, destinationRect, filter, sourceLayer, destinationLayer);
	}

	/// <summary>
	/// Blits the contents of this Texture to another Texture
	/// </summary>
	public void Blit(Texture destination, TextureFilter filter)
	{
		Blit(new(Size), destination, new(destination.Size), filter);
	}

	/// <summary>
	/// Clones this Texture and creates a new one with the same pixel data
	/// </summary>
	public Texture Clone()
	{
		var clone = new Texture(GraphicsDevice, Width, Height, Format);
		Blit(clone, TextureFilter.Nearest);
		return clone;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (!disposed)
		{
			// Targets should dispose their Texture Attachments
			if (!IsTargetAttachment)
				GraphicsDevice.DestroyResource(Resource);

			disposed = true;
		}
	}
}
