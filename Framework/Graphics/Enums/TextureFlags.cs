namespace Foster.Framework;

/// <summary>
/// Optional Texture Flags used when creating new Textures
/// </summary>
[Flags]
public enum TextureFlags
{
	None = 0,

	/// <summary>
	/// Allows the Texture to be used during Compute Storage Reads
	/// </summary>
	ComputeRead = 1 << 0,

	/// <summary>
	/// Allows the Texture to be attached to a <see cref="Target"/> created from existing
	/// Textures (<see cref="Target(GraphicsDevice, ReadOnlySpan{Texture}, int, string?)"/>).
	/// Color formats become color targets, depth formats depth targets.
	/// </summary>
	RenderTarget = 1 << 2,

	/// <summary>
	/// Lets shaders and blits read an external Texture (one wrapped from a native texture
	/// through SDL properties). Foster-created Textures are always readable; for external
	/// ones the native texture must allow shader reads, so this is opt-in.
	/// </summary>
	Sampled = 1 << 3,

	/// <summary>
	/// Allows the Texture to be used during Compute Storage Writes
	/// </summary>
	ComputeWrite = 1 << 1,
}