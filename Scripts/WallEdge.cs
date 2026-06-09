using System;

namespace MapEditorPrototype
{
    [Serializable]
    public struct WallEdge : IEquatable<WallEdge>
    {
        public int x;
        public int y;
        public WallOrientation orientation;

        public WallEdge(int x, int y, WallOrientation orientation)
        {
            this.x = x;
            this.y = y;
            this.orientation = orientation;
        }

        public bool Equals(WallEdge other)
        {
            return x == other.x && y == other.y && orientation == other.orientation;
        }

        public override bool Equals(object obj)
        {
            return obj is WallEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + (int)orientation;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y}, {orientation})";
        }
    }
}
