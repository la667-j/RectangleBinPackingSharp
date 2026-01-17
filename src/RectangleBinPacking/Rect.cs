using System;

namespace RectangleBinPacking
{
    /// <summary>
    /// Represents a 2D rectangle defined by its position (X, Y) and dimensions (Width, Height).
    /// </summary>
    public struct Rect
    {
        /// <summary>
        /// The x-coordinate of the left edge of the rectangle.
        /// </summary>
        public int X;

        /// <summary>
        /// The y-coordinate of the top edge of the rectangle.
        /// </summary>
        public int Y;

        /// <summary>
        /// The width of the rectangle.
        /// </summary>
        public int Width;

        /// <summary>
        /// The height of the rectangle.
        /// </summary>
        public int Height;

        /// <summary>
        /// Gets the x-coordinate of the right edge of the rectangle (X + Width).
        /// </summary>
        public int Right => X + Width;

        /// <summary>
        /// Gets the y-coordinate of the bottom edge of the rectangle (Y + Height).
        /// </summary>
        public int Bottom => Y + Height;

        /// <summary>
        /// Gets the surface area of the rectangle (Width * Height).
        /// </summary>
        public int Area => Width * Height;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct.
        /// </summary>
        /// <param name="x">The x-coordinate of the top-left corner.</param>
        /// <param name="y">The y-coordinate of the top-left corner.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public Rect(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        /// <summary>
        /// Determines whether this rectangle entirely contains the specified rectangle.
        /// </summary>
        /// <param name="other">The rectangle to evaluate.</param>
        /// <returns><c>true</c> if this rectangle entirely contains <paramref name="other"/>; otherwise, <c>false</c>.</returns>
        public bool Contains(Rect other)
        {
            return other.X >= X && other.Y >= Y &&
                   other.Right <= Right && other.Bottom <= Bottom;
        }

        /// <summary>
        /// Determines whether this rectangle intersects with the specified rectangle.
        /// </summary>
        /// <param name="other">The rectangle to evaluate.</param>
        /// <returns><c>true</c> if the two rectangles intersect; otherwise, <c>false</c>.</returns>
        public bool Intersects(Rect other)
        {
            return X < other.Right && Right > other.X &&
                   Y < other.Bottom && Bottom > other.Y;
        }
    }
}