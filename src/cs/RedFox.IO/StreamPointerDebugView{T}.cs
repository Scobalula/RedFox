// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace RedFox.IO;

/// <summary>
/// Provides a debugger visualizer for <see cref="StreamPointer{T}"/> that displays
/// the accessible items in the debugger watch window.
/// </summary>
/// <typeparam name="T">The unmanaged structure type visualized.</typeparam>
/// <remarks>
/// This class is not intended to be used directly. It is invoked automatically
/// by the debugger when inspecting <see cref="StreamPointer{T}"/> instances.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class StreamPointerDebugView<T> where T : unmanaged
{
    private const int DefaultPreviewCount = 4;
    private const int MaxPreviewCount = 32;

    private readonly StreamPointer<T> _collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamPointerDebugView{T}"/> class.
    /// </summary>
    /// <param name="collection">The <see cref="StreamPointer{T}"/> to visualize.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
    public StreamPointerDebugView(StreamPointer<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        _collection = collection;
    }

    /// <summary>
    /// Gets a preview of the items in the collection for debugger display.
    /// </summary>
    /// <remarks>
    /// Returns a limited number of items (up to <see cref="DefaultPreviewCount"/> by default,
    /// or fewer if the collection has fewer items). This prevents excessive memory usage
    /// when debugging large arrays.
    /// </remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items
    {
        get
        {
            int count = _collection.Count;

            if (count < 0)
            {
                count = DefaultPreviewCount;
            }
            else
            {
                count = Math.Min(count, MaxPreviewCount);
            }

            T[] items = new T[count];

            try
            {
                for (int i = 0; i < count; i++)
                {
                    items[i] = _collection[i];
                }
            }
            catch
            {
                // If reading fails (e.g., invalid pointers), return partial array
            }

            return items;
        }
    }

    /// <summary>
    /// Gets the base stream used by the collection.
    /// </summary>
    public Stream? BaseStream => _collection.BaseStream;

    /// <summary>
    /// Gets the base pointer offset in the stream.
    /// </summary>
    public long Pointer => _collection.Pointer;

    /// <summary>
    /// Gets the end offset of the accessible data range.
    /// </summary>
    public long EndOffset => _collection.EndOffset;

    /// <summary>
    /// Gets the number of items in the collection, or -1 if unbounded.
    /// </summary>
    public int Count => _collection.Count;

    /// <summary>
    /// Gets a value indicating whether this is a pointer-chased collection.
    /// </summary>
    public bool IsPointerArray => _collection.IsPointerArray;
}
