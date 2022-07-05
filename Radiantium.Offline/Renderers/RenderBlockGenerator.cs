namespace Radiantium.Offline.Renderers
{
    internal struct RenderBlock
    {
        public int OffsetX;
        public int OffsetY;
        public int SizeX;
        public int SizeY;
    }

    internal class RenderBlockGenerator
    {
        public enum Direction : int
        {
            Right, Down, Left, Up
        }

        int _blockX;
        int _blockY;
        readonly int _numBlockX;
        readonly int _numBlockY;
        readonly int _sizeX;
        readonly int _sizeY;
        readonly int _blockSize;
        readonly int _blockCount;
        int _numSteps;
        int _blocksLeft;
        int _stepsLeft;
        Direction _direction;
        readonly object _lock;

        public int BlockCount => _blockCount;

        public RenderBlockGenerator(int width, int height, int blockSize)
        {
            _sizeX = width;
            _sizeY = height;
            _blockSize = blockSize;
            _numBlockX = (int)Math.Ceiling(width / (float)blockSize);
            _numBlockY = (int)Math.Ceiling(height / (float)blockSize);
            _blockCount = _numBlockX * _numBlockY;
            _blocksLeft = BlockCount;
            _direction = Direction.Right;
            _blockX = _numBlockX / 2;
            _blockY = _numBlockY / 2;
            _stepsLeft = 1;
            _numSteps = 1;
            _lock = new object();
        }

        public bool Next(out RenderBlock block)
        {
            lock (_lock)
            {
                if (_blocksLeft == 0)
                {
                    block = default;
                    return false;
                }
                int posX = _blockX * _blockSize;
                int posY = _blockY * _blockSize;
                block.OffsetX = posX;
                block.OffsetY = posY;
                int sizeX = _sizeX - posX;
                int sizeY = _sizeY - posY;
                sizeX = Math.Min(sizeX, _blockSize);
                sizeY = Math.Min(sizeY, _blockSize);
                block.SizeX = sizeX;
                block.SizeY = sizeY;
                if (--_blocksLeft == 0)
                {
                    return true;
                }
                do
                {
                    switch (_direction)
                    {
                        case Direction.Up:
                            ++_blockY;
                            break;
                        case Direction.Down:
                            --_blockY;
                            break;
                        case Direction.Left:
                            --_blockX;
                            break;
                        case Direction.Right:
                            ++_blockX;
                            break;
                    }
                    if (--_stepsLeft == 0)
                    {
                        _direction = (Direction)(((int)_direction + 1) % 4);
                        if (_direction == Direction.Left || _direction == Direction.Right)
                        {
                            ++_numSteps;
                        }
                        _stepsLeft = _numSteps;
                    }
                } while ((_blockX < 0 || _blockY < 0) || (_blockX >= _numBlockX) || (_blockY >= _numBlockY));
            }
            return true;
        }
    }
}
